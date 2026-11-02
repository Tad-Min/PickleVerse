"""Create Unity playback assets from the supplied GIFs; never change the originals.

Requires Pillow and an FFmpeg executable (or imageio-ffmpeg).
Usage: python scripts/BuildFrontEndArtwork.py --ffmpeg path/to/ffmpeg.exe
"""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

from PIL import Image, ImageSequence


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg")
    args = parser.parse_args()
    if not args.ffmpeg:
        import imageio_ffmpeg
        args.ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    root = Path(__file__).resolve().parents[1]
    content = root / "My project/Assets/FrontEndContent"
    sources = [content / "Login/Login.gif", *sorted((content / "Loading/Artwork").glob("*.gif"))]
    report = []
    for source in sources:
        output = source.parent / "Playback"
        output.mkdir(exist_ok=True)
        movie, poster = output / f"{source.stem}.mp4", output / f"{source.stem}.jpg"
        with Image.open(source) as gif:
            width, height = gif.size
            count = gif.n_frames
            duration = sum(frame.info.get("duration", 100) for frame in ImageSequence.Iterator(gif)) / 1000
            gif.seek(0)
            gif.convert("RGB").save(poster, quality=94, optimize=True)
        # 25 fps preserves the source timing (including GIF disposal), caps decoder work,
        # and keeps H.264 dimensions even without stretching a 675px source.
        subprocess.run([args.ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
                        "-ignore_loop", "1", "-i", str(source), "-an", "-t", str(duration),
                        "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2:out_color_matrix=bt709:out_range=tv,fps=25,setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709", "-c:v", "libx264",
                        "-preset", "slow", "-crf", "19", "-pix_fmt", "yuv420p",
                        "-profile:v", "baseline", "-bf", "0", "-level", "3.1",
                        "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", "-color_range", "tv",
                        "-x264-params", "colorprim=bt709:transfer=bt709:colormatrix=bt709:fullrange=off",
                        "-movflags", "+faststart", str(movie)], check=True)
        metadata = subprocess.run([args.ffmpeg, "-hide_banner", "-i", str(movie)], capture_output=True, text=True)
        stream = next(line.strip() for line in metadata.stderr.splitlines() if "Video:" in line)
        if "baseline" not in stream.lower() or "bt709" not in stream or "unknown" in stream:
            raise RuntimeError(f"Unexpected playback metadata: {stream}")
        entry = dict(source=str(source.relative_to(root)), sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                     width=width, height=height, frames=count, durationSeconds=duration,
                     sourceBytes=source.stat().st_size, movieBytes=movie.stat().st_size, posterBytes=poster.stat().st_size)
        report.append(entry)
        print(json.dumps(entry))
    destination = root / "artifacts/animated-artwork-20261102"
    destination.mkdir(parents=True, exist_ok=True)
    (destination / "conversion.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
