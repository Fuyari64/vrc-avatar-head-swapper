import bpy
import bmesh
import process_armatures
from mathutils import Vector, Euler
import exporter

def convertToBlender(unity_coords, isPosition = False):
    x = unity_coords['x']  # X stays the same
    y = unity_coords['y']  # Unity Y (up) -> Blender Z (up)
    z = unity_coords['z']  # Unity Z (forward) -> Blender Y (forward)
    
    # For position the behaviour seems to be different
    if isPosition:
        return (x, -z, y)
    
    return (x, z, y)

def applyTransforms(object, data):
    if object.name not in bpy.context.view_layer.objects:
        bpy.context.collection.objects.link(object)

    object.location = Vector(convertToBlender(data["position"], True))
    object.rotation_euler = Euler(convertToBlender(data["rotation"]), 'XYZ')
    object.scale = Vector(convertToBlender(data["scale"]))
    
    clean_up_meshes(object, data['meshes'])

def clean_up_meshes(obj, kept_mesh_names, isHead = False):
    kept_meshes = []
    for child in obj.children:
        if child.type == 'MESH':
            if child.name in kept_mesh_names:
                kept_meshes.append(child)
            else:
                bpy.data.objects.remove(child, do_unlink=True)

    prune_bones(kept_meshes)

def prune_bones(kept_meshes):
        if not kept_meshes:
            return
        
        armature = None
        for mesh in kept_meshes:
            armature = mesh.find_armature()
            if armature:
                break
        if not armature:
            return

        def bone_is_near_meshes(bone_name: str) -> bool:
            """Check if a bone is inside the bounding box of any kept mesh"""
            bone = armature.data.bones.get(bone_name)
            if not bone:
                return False
            
            bone_world_pos = armature.matrix_world @ bone.head_local
            bone_tail_world_pos = armature.matrix_world @ bone.tail_local
            
            for mesh in kept_meshes:
                if mesh.type != 'MESH':
                    continue
                
                if mesh.bound_box:
                    world_bounds = [mesh.matrix_world @ Vector(bound) for bound in mesh.bound_box]
                    
                    min_x = min(bound.x for bound in world_bounds)
                    max_x = max(bound.x for bound in world_bounds)
                    min_y = min(bound.y for bound in world_bounds)
                    max_y = max(bound.y for bound in world_bounds)
                    min_z = min(bound.z for bound in world_bounds)
                    max_z = max(bound.z for bound in world_bounds)
                    
                    if (min_x <= bone_world_pos.x <= max_x and 
                        min_y <= bone_world_pos.y <= max_y and 
                        min_z <= bone_world_pos.z <= max_z):
                        return True
                    
                    if (min_x <= bone_tail_world_pos.x <= max_x and 
                        min_y <= bone_tail_world_pos.y <= max_y and 
                        min_z <= bone_tail_world_pos.z <= max_z):
                        return True
            
            return False

        def bone_has_weights(bone_name: str) -> bool:
            """Check if bone has vertex weights in any kept mesh"""
            for mesh in kept_meshes:
                vg = mesh.vertex_groups.get(bone_name)
                if not vg:
                    continue
                idx = vg.index
                for vertex in mesh.data.vertices:
                    for vGroup in vertex.groups:
                        if vGroup.group == idx and vGroup.weight > 0.0:
                            return True
            return False

        bones_to_delete = [bone.name for bone in armature.data.bones
                    if bone.use_deform and not bone_has_weights(bone.name) and not bone_is_near_meshes(bone.name)]
        if not bones_to_delete:
            return

        bpy.context.view_layer.objects.active = armature
        armature.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')

        editable_bones = armature.data.edit_bones
        existing_names = [name for name in bones_to_delete if name in editable_bones]

        # Sort by bones with most children first.
        delete_order = sorted(
            existing_names,
            key=lambda n: len(editable_bones[n].children),
            reverse=True,
        )

        for bone_name in delete_order:
            if bone_name in editable_bones:
                editable_bones.remove(editable_bones[bone_name])
        
        bpy.ops.object.mode_set(mode='OBJECT')

def apply(source, target, transforms):
    print(">>> PROCESSOR: applying data")

    applyTransforms(source, transforms['headAvatarMetadata'])
    applyTransforms(target, transforms['bodyAvatarMetadata'])

    final_obj = process_armatures.apply_transforms_and_merge_armatures(source, target)

    exporter.export_merged_avatar(final_obj)

    print(source)
    print(target)
    print(transforms)

