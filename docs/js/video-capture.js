// Client/wwwroot/js/videoCapture.js
let mediaStream = null;
let captureInterval = null;
let dotNetHelper = null;
let videoElement = null;

window.videoCapture = {
    startCapture: async function (helper, fps, resolution, quality) {
        console.log('🚀 Starting video capture...', { fps, quality, resolution });
        dotNetHelper = helper;
        
        try {
            // Configuration de la résolution
            const videoConstraints = {
                low: { width: 320, height: 240 },
                medium: { width: 640, height: 480 },
                high: { width: 1280, height: 720 }
            };
            
            const config = videoConstraints[resolution] || videoConstraints.medium;
            
            // Accéder à la webcam
            mediaStream = await navigator.mediaDevices.getUserMedia({ 
                video: { 
                    width: { ideal: config.width },
                    height: { ideal: config.height },
                    frameRate: { ideal: fps }
                } 
            });
            
            console.log('✅ Camera access granted');

            // Créer un élément vidéo
            videoElement = document.createElement('video');
            videoElement.srcObject = mediaStream;
            videoElement.playsInline = true;
            videoElement.muted = true;
            
            // Attendre que la vidéo soit prête
            await new Promise((resolve) => {
                videoElement.onloadedmetadata = () => {
                    videoElement.play();
                    resolve();
                };
            });

            const canvas = document.createElement('canvas');
            const context = canvas.getContext('2d');
            canvas.width = config.width;
            canvas.height = config.height;

            let frameCounter = 0;

            // Démarrer la capture des frames
            captureInterval = setInterval(async () => {
                if (videoElement.readyState === videoElement.HAVE_ENOUGH_DATA) {
                    context.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
                    const frameData = canvas.toDataURL('image/jpeg', quality);
                    const base64Data = frameData.split(',')[1];
                    
                    frameCounter++;
                    
                    try {
                        await dotNetHelper.invokeMethodAsync('ReceiveVideoFrame', base64Data);
                    } catch (error) {
                        console.error('Error sending frame:', error);
                    }
                }
            }, 1000 / fps);

            console.log('🎥 Video capture started successfully');
            return true;
            
        } catch (error) {
            console.error('❌ Error starting video capture:', error);
            return false;
        }
    },

    stopCapture: function() {
        console.log('🛑 Stopping video capture...');
        
        if (captureInterval) {
            clearInterval(captureInterval);
            captureInterval = null;
        }
        
        if (mediaStream) {
            mediaStream.getTracks().forEach(track => track.stop());
            mediaStream = null;
        }
        
        if (videoElement) {
            videoElement.srcObject = null;
            videoElement = null;
        }
        
        dotNetHelper = null;
        console.log('✅ Video capture stopped');
    }
};