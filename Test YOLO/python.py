from ultralytics import YOLO

model = YOLO("best.pt")

model.predict(
    source="test1.mp4",
    conf=0.5,
    iou=0.5,
    show=True,
    save=True
)
