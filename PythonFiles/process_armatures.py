import bpy
from mathutils import Vector
import merge_armature

def identify_humanoid_bones(name: str):
    n = name.casefold()
    if 'upperchest' in n or ('chest' in n): return 'Chest'
    if 'spine' in n: return 'Spine'
    if 'hips' in n or 'pelvis' in n: return 'Hips'
    if 'neck' in n: return 'Neck'
    if 'head' in n: return 'Head'
    return None

def index_humanoid_bones(armature):
    slots = {}
    for bone in armature.data.bones:
        slot = identify_humanoid_bones(bone.name)
        if slot and slot not in slots:
            slots[slot] = bone.name
    return slots

def build_head_to_body_map(head_arm, body_arm):
    head_slots = index_humanoid_bones(head_arm)
    body_slots = index_humanoid_bones(body_arm)
    intersectBones = ['Hips', 'Spine', 'Chest', 'Neck', 'Head']
    return {
        head_slots[k]: body_slots[k]
        for k in intersectBones
        if k in head_slots and k in body_slots
    }

def align_humanoid_vertebra(head_arm, body_arm):
    bone_map = build_head_to_body_map(head_arm, body_arm)
    if not bone_map:
        return

    body_world = body_arm.matrix_world
    target_positions = {}

    for head_slot_name, body_slot_name in bone_map.items():
        body_bone = body_arm.data.bones.get(body_slot_name)
        if body_bone:
            head_world = body_world @ body_bone.head_local
            tail_world = body_world @ body_bone.tail_local
            target_positions[head_slot_name] = (head_world, tail_world)
    
    if not target_positions:
        return

    apply_positions(head_arm, target_positions)

def apply_positions(head_armature, target_positions):
    bpy.context.view_layer.objects.active = head_armature
    head_armature.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')

    try:
        world_to_local = head_armature.matrix_world.inverted()
        edit_bones = head_armature.data.edit_bones

        for bone_name, (head_world, tail_world) in target_positions.items():
            edit_bone = edit_bones.get(bone_name)
            if edit_bone:
                head_local = world_to_local @ head_world
                tail_local = world_to_local @ tail_world

                edit_bone.head = head_local
                edit_bone.tail = tail_local

    finally:
        bpy.ops.object.mode_set(mode='OBJECT')
        bpy.context.view_layer.update()

def _find_armature_in_object(obj):
    if obj.type == 'ARMATURE':
        return obj
    
    for child in obj.children_recursive:
        if child.type == 'ARMATURE':
            return child

    return None

def _apply_transforms_to_object(obj):
    """Apply all transforms to an object (equivalent to Ctrl+A → All Transforms)"""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def apply_transforms_and_merge_armatures(source_obj, target_obj):
    head_armature = _find_armature_in_object(source_obj)
    body_armature = _find_armature_in_object(target_obj)
    
    if head_armature and body_armature:
        align_humanoid_vertebra(head_armature, body_armature)
        print(f"Aligned bones between {head_armature.name} and {body_armature.name}")
    else:
        print("Could not find armatures in one or both objects")

    _apply_transforms_to_object(source_obj)
    _apply_transforms_to_object(target_obj)
    
    print("Merging armatures...")
    final_armature = merge_armature.merge_armatures(head_armature, body_armature)
    print(f"Merged armature: {final_armature.name}")

    return target_obj
