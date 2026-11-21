"""Join rendered PNG frames into an H.264 MP4 with Blender's sequencer (no external ffmpeg needed).

blender --background --factory-startup --python encode_mp4.py -- <frames_dir> <out.mp4> [fps]
"""
import bpy, glob, os, sys

args = sys.argv[sys.argv.index("--") + 1:]
frames_dir, out = args[0], args[1]
fps = int(args[2]) if len(args) > 2 else 30
files = sorted(os.path.basename(f) for f in glob.glob(os.path.join(frames_dir, "f_*.png")))
if not files:
    raise SystemExit("no frames in " + frames_dir)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = fps
scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
scene.render.resolution_percentage = 100
scene.frame_start, scene.frame_end = 1, len(files)
se = scene.sequence_editor_create()
coll = se.strips if hasattr(se, "strips") else se.sequences   # renamed to "strips" in Blender 5
strip = coll.new_image("Frames", os.path.join(frames_dir, files[0]), 1, 1)
for name in files[1:]:
    strip.elements.append(name)
scene.render.image_settings.media_type = "VIDEO"
scene.render.image_settings.file_format = "FFMPEG"
ff = scene.render.ffmpeg
ff.format = "MPEG4"
ff.codec = "H264"
ff.constant_rate_factor = "HIGH"
ff.ffmpeg_preset = "GOOD"
ff.audio_codec = "NONE"
scene.view_settings.view_transform = "Standard"   # frames are already display-referred
scene.render.filepath = out
bpy.ops.render.render(animation=True)
print("ENCODED", out, len(files), "frames")
