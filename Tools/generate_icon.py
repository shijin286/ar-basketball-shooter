"""
AR Basketball Shooter - app icon (Stage 8).

Draws a 1024 x 1024 PNG: a shaded basketball with seams, framed by white
"AR scanning" corner brackets on a dark blue background. Standard library only.

Usage:   python generate_icon.py [output.png]
Default: ../Assets/UI/AppIcon.png
"""
import math
import os
import struct
import sys
import zlib

SIZE = 1024
BG_TOP, BG_BOTTOM = (16, 26, 51), (30, 47, 92)
BALL = (222, 96, 28)
SEAM = (25, 15, 10)
WHITE = (245, 245, 245)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def smooth(edge0, edge1, x):
    t = max(0.0, min(1.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3 - 2 * t)


def rotate(p, ax, az):
    x, y, z = p
    # around Z, then around X - tilts the seams into a nice 3/4 view
    x, y = x * math.cos(az) - y * math.sin(az), x * math.sin(az) + y * math.cos(az)
    y, z = y * math.cos(ax) - z * math.sin(ax), y * math.sin(ax) + z * math.cos(ax)
    return x, y, z


def seam_distance(p):
    """Same pattern as the in-game ball texture (BasketballPrefabBuilder.SeamDistance)."""
    x, y, _ = p
    curve_x = 0.7 - 0.25 * y * y
    return min(abs(y), abs(x), abs(abs(x) - curve_x))


def bracket_alpha(px, py):
    """White L-shaped corners around the ball (an AR scanning frame)."""
    c, half, arm, width = SIZE / 2, SIZE * 0.40, SIZE * 0.13, SIZE * 0.028
    alpha = 0.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = c + sx * half, c + sy * half  # outer corner
            dx, dy = (px - cx) * -sx, (py - cy) * -sy  # distance inwards from the corner
            horizontal = 0 <= dx <= arm and 0 <= dy <= width
            vertical = 0 <= dy <= arm and 0 <= dx <= width
            if horizontal or vertical:
                alpha = 1.0
    return alpha


def pixel(px, py):
    colour = mix(BG_TOP, BG_BOTTOM, py / SIZE)

    # brackets (with a 2x2 supersample for smooth edges)
    a = sum(bracket_alpha(px + ox, py + oy) for ox in (0.25, 0.75) for oy in (0.25, 0.75)) / 4
    colour = mix(colour, WHITE, a)

    # ball
    radius = SIZE * 0.30
    nx, ny = (px + 0.5 - SIZE / 2) / radius, (SIZE / 2 - py - 0.5) / radius
    r2 = nx * nx + ny * ny
    edge = 1.0 - smooth(1.0 - 2.0 / radius, 1.0, math.sqrt(r2))
    if edge > 0:
        nz = math.sqrt(max(0.0, 1.0 - r2))
        light = max(0.0, nx * -0.45 + ny * 0.55 + nz * 0.70)  # light from the top-left
        shade = 0.35 + 0.75 * light
        ball = tuple(min(255, c * shade) for c in BALL)

        seam = 1.0 - smooth(0.012, 0.024, seam_distance(rotate((nx, ny, nz), 0.45, 0.5)))
        ball = mix(ball, SEAM, seam)

        highlight = smooth(0.82, 0.97, light) * 0.35
        ball = mix(ball, WHITE, highlight)
        colour = mix(colour, ball, edge)

    return bytes(int(round(c)) for c in colour)


def write_png(path, rows):
    raw = b"".join(b"\x00" + row for row in rows)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0)))  # 8-bit RGB
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Assets", "UI", "AppIcon.png")
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    rows = [b"".join(pixel(x, y) for x in range(SIZE)) for y in range(SIZE)]
    write_png(path, rows)
    print(f"Wrote {path} ({os.path.getsize(path) // 1024} KB, {SIZE}x{SIZE})")


if __name__ == "__main__":
    main()
