import bpy
import os

def export_for_unity(armature_obj, mesh_objects, include_empties=True):
    """Export armature, meshes, and empty objects for Unity with correct FBX settings"""
    
    # Select armature and meshes
    bpy.ops.object.select_all(action='DESELECT')
    armature_obj.select_set(True)
    for mesh in mesh_objects:
        mesh.select_set(True)
    
    # Hardcoded path to ../Temp
    script_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else ""
    temp_dir = os.path.join(script_dir, "Temp")
    filepath = os.path.join(temp_dir, "merged_avatar2.fbx")
    
    # Export with Unity settings
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        global_scale=1.0,  # Make sure this is 1.0
        apply_scale_options='FBX_SCALE_ALL',
        apply_unit_scale=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        axis_forward='-Z',                       # Unity’s forward
        axis_up='Y'                              # Unity’s up
    )
    
    print(f"Exported to: {filepath}")

def export_merged_avatar(final_avatar_obj):
    """Export the final merged avatar for Unity"""
    
    armature = final_avatar_obj
    meshes = [obj for obj in final_avatar_obj.children_recursive if obj.type == 'MESH']
    
    if armature and meshes:
        export_for_unity(armature, meshes)
    else:
        print("Error: No armature or meshes found")
