"""Build-time only: send four fixed sentences to Edge TTS, never player data."""
import argparse
import asyncio
from pathlib import Path
import edge_tts

VOICE = "vi-VN-HoaiMyNeural"
PHRASES = {
    "InsufficientData": "Chưa thấy rõ động tác. Hãy giữ vai, hông và tay trong khung hình.",
    "StraightKnees": "Thử chùng gối nhẹ ở tư thế chuẩn bị.",
    "ShortFinish": "Thử hoàn tất động tác vung vợt.",
    "Recorded": "Đã ghi nhận cú đánh. Tiếp tục giữ nhịp nhé.",
}

async def generate(force=False, dry_run=False):
    if dry_run:
        for name, text in PHRASES.items():
            print(name, VOICE, text)
        return
    voices = await asyncio.wait_for(edge_tts.list_voices(), timeout=30)
    if not any(v["ShortName"] == VOICE and v["Locale"] == "vi-VN" for v in voices):
        raise RuntimeError("Requested Vietnamese voice is unavailable; no substitute selected.")
    root = Path(__file__).resolve().parents[1]
    output = root / "Assets" / "OfflineCoach" / "Voice"
    staging = root / "Library" / "CoachTTSOutput"
    output.mkdir(parents=True, exist_ok=True)
    staging.mkdir(parents=True, exist_ok=True)
    for name, text in PHRASES.items():
        target = output / (name + ".mp3")
        if target.exists() and not force:
            print("Keep existing:", target.name)
            continue
        temporary = staging / (name + ".mp3")
        await asyncio.wait_for(edge_tts.Communicate(text, VOICE).save(str(temporary)), timeout=60)
        if temporary.stat().st_size < 1000:
            raise RuntimeError("Audio response too small: " + name)
        temporary.replace(target)
        print("Generated:", target.name, target.stat().st_size, "bytes")
    print("Import clips and refresh voice references in Unity. Listen before accepting speech QA.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    asyncio.run(generate(args.force, args.dry_run))

