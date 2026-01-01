#!/usr/bin/env python3
"""
SmartNest YOLO Detection Server
Détection automatique des poussins via YOLOv8
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import base64
import cv2
import numpy as np
import logging
import time
import sys
import os

# Configuration des logs
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.FileHandler('yolo_server.log')
    ]
)
logger = logging.getLogger(__name__)

app = Flask(__name__)
CORS(app)  # Autoriser les requêtes cross-origin

# Variables globales
model = None
model_loaded = False

def load_model():
    """Charger le modèle YOLO"""
    global model, model_loaded
    try:
        logger.info("🔄 Chargement du modèle YOLO...")
        
        # Essayer d'importer ultralytics
        try:
            from ultralytics import YOLO
        except ImportError:
            logger.error("❌ ultralytics n'est pas installé. Installez-le avec: pip install ultralytics")
            return False
        
        # Charger le modèle
        model_path = os.getenv('YOLO_MODEL_PATH', 'yolov8n.pt')
        model = YOLO(model_path)
        
        # Test du modèle
        dummy_img = np.zeros((640, 640, 3), dtype=np.uint8)
        _ = model(dummy_img, verbose=False)
        
        model_loaded = True
        logger.info(f"✅ Modèle YOLO chargé: {model_path}")
        return True
        
    except Exception as e:
        logger.error(f"❌ Erreur lors du chargement du modèle: {e}")
        model_loaded = False
        return False

@app.route('/health', methods=['GET'])
def health_check():
    """Endpoint de santé pour vérifier que le serveur fonctionne"""
    return jsonify({
        'status': 'healthy' if model_loaded else 'degraded',
        'model_loaded': model_loaded,
        'timestamp': time.time()
    }), 200 if model_loaded else 503

@app.route('/detect', methods=['POST'])
def detect():
    """Endpoint principal de détection"""
    start_time = time.time()
    
    try:
        if not model_loaded:
            return jsonify({
                'error': 'Model not loaded',
                'detections': []
            }), 503
        
        # Récupérer les données
        data = request.json
        if not data or 'image' not in data:
            return jsonify({'error': 'No image provided'}), 400
        
        image_b64 = data['image']
        confidence_threshold = data.get('confidence', 0.5)
        
        # Décoder l'image base64
        try:
            # Supprimer le préfixe data:image si présent
            if ',' in image_b64:
                image_b64 = image_b64.split(',')[1]
            
            img_data = base64.b64decode(image_b64)
            nparr = np.frombuffer(img_data, np.uint8)
            img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            
            if img is None:
                return jsonify({'error': 'Failed to decode image'}), 400
                
        except Exception as e:
            logger.error(f"❌ Erreur décodage image: {e}")
            return jsonify({'error': f'Image decode error: {str(e)}'}), 400
        
        # Détection YOLO
        logger.info(f"🔍 Analyse d'une image {img.shape}")
        results = model(img, conf=confidence_threshold, verbose=False)
        
        # Extraire les détections
        detections = []
        for r in results:
            for box in r.boxes:
                x, y, w, h = box.xywh[0].tolist()
                detections.append({
                    'class': 'healthy_chicken',
                    'confidence': float(box.conf[0]),
                    'boundingBox': {
                        'x': float(x),
                        'y': float(y),
                        'width': float(w),
                        'height': float(h)
                    }
                })
        
        processing_time = (time.time() - start_time) * 1000  # en ms
        
        logger.info(f"✅ {len(detections)} détections en {processing_time:.0f}ms")
        
        return jsonify({
            'detections': detections,
            'processingTime': processing_time,
            'modelVersion': 'yolov8n'
        }), 200
        
    except Exception as e:
        logger.error(f"❌ Erreur détection: {e}")
        return jsonify({
            'error': str(e),
            'detections': []
        }), 500

@app.route('/info', methods=['GET'])
def info():
    """Informations sur le serveur"""
    return jsonify({
        'name': 'SmartNest YOLO Server',
        'version': '1.0.0',
        'model_loaded': model_loaded,
        'endpoints': ['/health', '/detect', '/info']
    })

def main():
    """Point d'entrée principal"""
    logger.info("=" * 60)
    logger.info("🐣 SmartNest YOLO Detection Server")
    logger.info("=" * 60)
    
    # Charger le modèle
    if not load_model():
        logger.warning("⚠️ Serveur démarré SANS modèle YOLO")
        logger.warning("Le serveur acceptera les requêtes mais retournera des erreurs")
    
    # Démarrer le serveur Flask
    port = int(os.getenv('YOLO_PORT', 5000))
    host = os.getenv('YOLO_HOST', '0.0.0.0')
    
    logger.info(f"🚀 Démarrage du serveur sur http://{host}:{port}")
    logger.info("Endpoints disponibles:")
    logger.info(f"  - http://{host}:{port}/health")
    logger.info(f"  - http://{host}:{port}/detect")
    logger.info(f"  - http://{host}:{port}/info")
    logger.info("=" * 60)
    
    app.run(host=host, port=port, debug=False, threaded=True)

if __name__ == '__main__':
    main()