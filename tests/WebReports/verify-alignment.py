"""Render the native alignment fixture PDF and check the actual picture positions.

Usage: python verify-alignment.py path/to/WebReports/artifacts
Requires pypdfium2, Pillow and numpy. The PNG is also a visual-review artifact.
"""
from pathlib import Path
import sys
import numpy as np
import pypdfium2 as pdfium

root = Path(sys.argv[1])
with pdfium.PdfDocument(root / "chart-alignment.pdf") as pdf:
    assert len(pdf) == 1, f"Expected one page, got {len(pdf)}"
    page = pdf[0]
    bitmap = page.render(scale=2)
    image = bitmap.to_pil().convert("RGB")
    image.save(root / "chart-alignment-pdf.png")
    pixels = np.asarray(image).astype(int)
    boxes = []
    for colour in [(221,34,17), (17,187,51), (34,68,221), (255,180,0)]:
        mask = (np.abs(pixels - colour) < 12).all(axis=2)
        y,x = np.where(mask)
        assert len(x) > 100, f"Missing chart colour {colour}"
        boxes.append((int(x.min()), int(y.min()), int(x.max()), int(y.max())))
    left, centre, right, raster = boxes
    assert left[0] < centre[0] < right[0], boxes
    assert abs((centre[0]+centre[2]) - (left[0]+right[2])) <= 3, boxes
    assert abs(right[2]-raster[2]) <= 2, boxes
    assert abs((left[2]-left[0]) - (right[2]-right[0])) <= 2, boxes
    assert all(a[3] < b[1] for a,b in zip(boxes, boxes[1:])), boxes
    print(f"PASS PDF rendered chart alignment and size: {boxes}")
    bitmap.close()
    page.close()
