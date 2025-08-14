import bpy
import json

def load(path):
    print(">>> LOADER: loading", path)
    with open(path, "r") as f:
        return json.load(f)

def import_fbx(path):
    print(">>> LOADER: loading FBX")
    
    bpy.ops.import_scene.fbx(filepath=path)
    objects = bpy.context.selected_objects
    obj = objects[0] if objects else None
    
    if obj:
        # Apply all transforms to "bake" them into the geometry
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        
        print(f"After transform apply - location: {obj.location}")
        print(f"After transform apply - matrix_world: {obj.matrix_world.translation}")
        print(f"Applied transforms to {obj.name}")
    
    return obj
