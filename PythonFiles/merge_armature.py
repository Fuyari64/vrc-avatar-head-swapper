import bpy

def merge_armatures(source_arm, target_arm):
    """
    Merge source armature into target armature using Cats plugin method
    """
    
    # Step 1: Copy ALL bones from source to target armature
    copy_all_bones_to_target(source_arm, target_arm)
    
    # Step 2: Update all vertex group references to point to target armature
    update_vertex_group_references(source_arm, target_arm)
    
    # Step 3: Remove source armature
    bpy.data.objects.remove(source_arm, do_unlink=True)

    # Step 4: Clean bone names
    # clean_bone_names(target_arm)
    
    # Step 5: Clean armature name
    target_arm.name = target_arm.name.split('.')[0] if '.' in target_arm.name else target_arm.name
    
    # Step 6: Return target armature 
    return target_arm

def copy_all_bones_to_target(source_arm, target_arm):
    """Copy bones from source to target armature, preserving target's parenting structure"""
    
    bpy.context.view_layer.objects.active = target_arm
    target_arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    
    try:
        target_edit_bones = target_arm.data.edit_bones
        source_bones = source_arm.data.bones
        
        original_parents = {}
        for bone in target_arm.data.bones:
            if bone.parent:
                original_parents[bone.name] = bone.parent.name
            else:
                original_parents[bone.name] = None
        
        for source_bone in source_bones:
            if source_bone.name in target_edit_bones:
                target_edit_bones.remove(target_edit_bones[source_bone.name])
        
        for source_bone in source_bones:
            new_bone = target_edit_bones.new(source_bone.name)
            new_bone.head = source_bone.head_local
            new_bone.tail = source_bone.tail_local
            new_bone.roll = 0.0  # Default roll
        
        for source_bone in source_bones:
            if source_bone.name in target_edit_bones:
                target_bone = target_edit_bones[source_bone.name]
                
                # Priority 1: Use target armature's existing relationship
                if source_bone.name in original_parents:
                    parent_name = original_parents[source_bone.name]
                    if parent_name and parent_name in target_edit_bones:
                        target_bone.parent = target_edit_bones[parent_name]
                
                # Priority 2: If no target relationship, use source armature's relationship
                elif source_bone.parent and source_bone.parent.name in target_edit_bones:
                    target_bone.parent = target_edit_bones[source_bone.parent.name]

        # AFTER setting up source bone parents, restore ALL original target relationships
        for bone_name, parent_name in original_parents.items():
            if bone_name in target_edit_bones and parent_name and parent_name in target_edit_bones:
                target_edit_bones[bone_name].parent = target_edit_bones[parent_name]
                    
    finally:
        bpy.ops.object.mode_set(mode='OBJECT')

def update_vertex_group_references(source_arm, target_arm):
    """Update all meshes to reference the target armature - exactly like Cats"""
    
    source_meshes = [obj for obj in source_arm.children if obj.type == 'MESH']
    
    for mesh_obj in source_meshes:
        for modifier in mesh_obj.modifiers:
            if modifier.type == 'ARMATURE' and modifier.object == source_arm:
                modifier.object = target_arm
                break
        
        mesh_obj.parent = target_arm

# Disable for now, affecting unity import logic
def clean_bone_names(armature):
    """Remove .00X suffixes from bone names after merging, but only if no conflicts"""
    
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    
    try:
        edit_bones = armature.data.edit_bones
        
        for bone in edit_bones:
            if '.' in bone.name and bone.name.split('.')[-1].isdigit():
                clean_name = bone.name.rsplit('.', 1)[0]
                
                if clean_name not in edit_bones:
                    bone.name = clean_name
                else:
                    print(f"Keeping bone {bone.name} to avoid conflict with {clean_name}")
                    
    finally:
        bpy.ops.object.mode_set(mode='OBJECT')
