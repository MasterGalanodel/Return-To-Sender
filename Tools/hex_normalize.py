"""Normalize hex tile art from different sources to one regular hexagon of a fixed size.

Usage:
    python Tools/hex_normalize.py IN.png [IN2.png ...] -o OUT_DIR [--size 128] [--orientation flat]
                                  [--source-orientation auto] [--mask exact|keep] [--bleed 6]

Needs Pillow and numpy (pip install pillow numpy).

Pipeline, in standard image-processing terms:
  1. Trim       crop to the content bounding box of the alpha channel, ignoring stray specks.
  2. Orient     detect flat-top vs pointy-top and rotate 90 degrees when it differs from the target.
  3. Resample   non-uniform (anisotropic) scale of the bounding box to the regular hex's bounding box,
                done in premultiplied alpha with a Lanczos filter so transparent pixels can't leave dark halos.
                Any axis-aligned hex, regular or squashed (e.g. an "isometric" hex), becomes regular here,
                because a hex's bounding box fixes its shape.
  4. Mask       replace the alpha with an exact, supersampled (anti-aliased) regular-hex coverage mask,
                so every tile has an identical silhouette and rotated copies tile without seams.
  5. Bleed      dilate opaque colours outward a few pixels (edge padding / alpha bleeding) so filtering
                and mipmaps don't sample black from beyond the rim.

Geometry of a regular hexagon with circumradius R (centre to corner = side length):
  flat-top:   width = 2R (corner to corner),  height = sqrt(3) R (flat edge to flat edge)
  pointy-top: width = sqrt(3) R,              height = 2R
  inradius (apothem) = sqrt(3)/2 R.  So the aspect ratio is always 2 : sqrt(3) (about 1 : 0.866).
"""
import argparse
import os

import numpy as np
from PIL import Image

SQRT3 = 3 ** 0.5


def content_bbox(alpha, threshold=32, min_fraction=0.02):
    """Bounding box (left, top, right, bottom) of opaque content.

    A row or column counts only when at least min_fraction of it is opaque, so specks and stray strokes drop out.
    """
    solid = alpha > threshold
    rows = np.where(solid.sum(1) > solid.shape[1] * min_fraction)[0]
    cols = np.where(solid.sum(0) > solid.shape[0] * min_fraction)[0]
    if rows.size == 0 or cols.size == 0:
        raise ValueError('image has no opaque content')
    return int(cols.min()), int(rows.min()), int(cols.max()) + 1, int(rows.max()) + 1


def detect_orientation(alpha, threshold=32):
    """'flat' or 'pointy' for an image already trimmed to its hex.

    A flat-top hex has a flat edge along its top row (about half the width is opaque) and a single
    corner at the middle of its left column; a pointy-top hex is the reverse.
    """
    solid = alpha > threshold
    h, w = solid.shape
    band_y, band_x = max(1, h // 30), max(1, w // 30)
    top = solid[:band_y].mean()
    left = solid[:, :band_x].mean()
    return 'flat' if top >= left else 'pointy'


def hex_size(size, orientation):
    """(width, height) in pixels of a regular hex whose corner-to-corner span is `size`."""
    short = round(size * SQRT3 / 2)
    return (size, short) if orientation == 'flat' else (short, size)


def hex_mask(w, h, orientation, supersample=4):
    """Anti-aliased coverage (0..1) of a regular hex filling a w x h canvas."""
    if orientation == 'pointy':
        return hex_mask(h, w, 'flat', supersample).T
    s = supersample
    ys, xs = np.mgrid[0:h * s, 0:w * s]
    # Normalise to a hex of circumradius 1: x in [-1, 1], y in [-sqrt3/2, sqrt3/2].
    x = ((xs + .5) / (w * s) - .5) * 2
    y = ((ys + .5) / (h * s) - .5) * SQRT3
    inside = (np.abs(y) <= SQRT3 / 2) & (np.abs(x) <= 1 - np.abs(y) / SQRT3)
    return inside.reshape(h, s, w, s).mean((1, 3))


def resample_premultiplied(im, size):
    """Lanczos resize in premultiplied alpha; returns float RGBA in 0..1 (straight alpha)."""
    a = np.asarray(im, dtype=np.float32) / 255
    a[..., :3] *= a[..., 3:]
    pre = Image.fromarray((a * 255).round().astype(np.uint8))
    out = np.asarray(pre.resize(size, Image.LANCZOS), dtype=np.float32) / 255
    alpha = np.clip(out[..., 3:], 0, 1)
    out[..., :3] = np.where(alpha > 0, out[..., :3] / np.maximum(alpha, 1e-6), 0)
    out[..., 3:] = alpha
    return np.clip(out, 0, 1)


def bleed(rgb, known, steps):
    """Grow known colours into unknown pixels, one 4-neighbour ring per step (edge padding)."""
    rgb, known = rgb.copy(), known.copy()
    for _ in range(steps):
        if known.all():
            break
        acc = np.zeros_like(rgb)
        cnt = np.zeros(known.shape, np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            k = np.roll(known, (dy, dx), (0, 1))
            acc += np.roll(rgb, (dy, dx), (0, 1)) * k[..., None]
            cnt += k
        grow = ~known & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        known |= grow
    return rgb


def normalize(src, dst, size=128, orientation='flat', source_orientation='auto', mask='exact', bleed_px=6):
    im = Image.open(src).convert('RGBA')
    box = content_bbox(np.asarray(im)[..., 3])
    im = im.crop(box)

    found = detect_orientation(np.asarray(im)[..., 3]) if source_orientation == 'auto' else source_orientation
    if found != orientation:
        im = im.transpose(Image.Transpose.ROTATE_90)

    w, h = hex_size(size, orientation)
    out = resample_premultiplied(im, (w, h))
    if mask == 'exact':
        known = out[..., 3] > .5
        out[..., 3] = hex_mask(w, h, orientation)
        out[..., :3] = bleed(out[..., :3], known, bleed_px)
    else:
        out[..., :3] = bleed(out[..., :3], out[..., 3] > .5, bleed_px)

    os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
    Image.fromarray((out * 255).round().astype(np.uint8)).save(dst, optimize=True)
    print(f'{src}: content {box}, {found}-top -> {dst} {w}x{h} {orientation}-top '
          f'(Pixels Per Unit {size} makes it 1 unit corner to corner)')


def main():
    p = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    p.add_argument('inputs', nargs='+')
    p.add_argument('-o', '--out-dir', required=True)
    p.add_argument('--size', type=int, default=128, help='corner-to-corner span in pixels (2 x circumradius)')
    p.add_argument('--orientation', choices=('flat', 'pointy'), default='flat', help='target orientation')
    p.add_argument('--source-orientation', choices=('auto', 'flat', 'pointy'), default='auto')
    p.add_argument('--mask', choices=('exact', 'keep'), default='exact',
                   help="exact: stamp a perfect hex silhouette (ground tiles); keep: keep the art's own alpha "
                        '(props such as fences that overhang or leave gaps)')
    p.add_argument('--bleed', type=int, default=6, help='edge padding in pixels')
    a = p.parse_args()
    for src in a.inputs:
        dst = os.path.join(a.out_dir, os.path.basename(src))
        normalize(src, dst, a.size, a.orientation, a.source_orientation, a.mask, a.bleed)


if __name__ == '__main__':
    main()
