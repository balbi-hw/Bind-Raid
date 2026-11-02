import bpy
import addon_utils
addon_utils.enable('blender_mcp', default_set=False)
def connect():
    if not getattr(bpy.types, 'blendermcp_server', None):
        bpy.ops.blendermcp.start_server()
    return None
bpy.app.timers.register(connect, first_interval=2)
