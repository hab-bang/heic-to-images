from __future__ import annotations

import struct
from pathlib import Path


SIZES = (16, 24, 32, 48, 64, 128, 256)
WHITE = (255, 255, 255, 255)
BLACK = (0, 0, 0, 255)


def fill_rect(pixels: list[list[tuple[int, int, int, int]]], rect: tuple[float, float, float, float]) -> None:
    size = len(pixels)
    left = max(0, min(size, round(rect[0] * size)))
    top = max(0, min(size, round(rect[1] * size)))
    right = max(0, min(size, round(rect[2] * size)))
    bottom = max(0, min(size, round(rect[3] * size)))

    for y in range(top, bottom):
        row = pixels[y]
        for x in range(left, right):
            row[x] = WHITE


def draw_icon(size: int) -> bytes:
    pixels = [[BLACK for _ in range(size)] for _ in range(size)]

    margin_x = 0.17
    margin_y = 0.18
    glyph_height = 1 - (2 * margin_y)
    thickness = 0.105

    h_left = margin_x
    h_right = 0.50
    i_left = 0.60
    i_right = 0.83
    top = margin_y
    bottom = margin_y + glyph_height
    middle = 0.50

    fill_rect(pixels, (h_left, top, h_left + thickness, bottom))
    fill_rect(pixels, (h_right - thickness, top, h_right, bottom))
    fill_rect(pixels, (h_left, middle - thickness / 2, h_right, middle + thickness / 2))

    fill_rect(pixels, (i_left, top, i_right, top + thickness))
    fill_rect(pixels, ((i_left + i_right - thickness) / 2, top, (i_left + i_right + thickness) / 2, bottom))
    fill_rect(pixels, (i_left, bottom - thickness, i_right, bottom))

    return to_dib(pixels)


def to_dib(pixels: list[list[tuple[int, int, int, int]]]) -> bytes:
    size = len(pixels)
    xor_rows = []
    for y in range(size - 1, -1, -1):
        row = bytearray()
        for red, green, blue, alpha in pixels[y]:
            row.extend((blue, green, red, alpha))
        xor_rows.append(bytes(row))

    mask_stride = ((size + 31) // 32) * 4
    and_mask = b"\x00" * mask_stride * size
    xor_bitmap = b"".join(xor_rows)

    header = struct.pack(
        "<IIIHHIIIIII",
        40,
        size,
        size * 2,
        1,
        32,
        0,
        len(xor_bitmap),
        0,
        0,
        0,
        0,
    )
    return header + xor_bitmap + and_mask


def write_ico(path: Path) -> None:
    images = [(size, draw_icon(size)) for size in SIZES]
    header_size = 6 + (16 * len(images))
    offset = header_size

    directory = bytearray()
    payload = bytearray()
    for size, image in images:
        directory.extend(
            struct.pack(
                "<BBBBHHII",
                0 if size == 256 else size,
                0 if size == 256 else size,
                0,
                0,
                1,
                32,
                len(image),
                offset,
            )
        )
        payload.extend(image)
        offset += len(image)

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(struct.pack("<HHH", 0, 1, len(images)) + directory + payload)


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[1]
    write_ico(root / "src" / "HeicToImages.App" / "Assets" / "app-icon.ico")
