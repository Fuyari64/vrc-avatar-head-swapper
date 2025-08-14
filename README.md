# VRC Avatar Head Swapper

A Unity Editor tool for VRChat avatars that aims to automate the head swapping process using blender, which is usually necessary for face tracked avatars.

## Features

- **Head-Body Merging**: Combine head and body avatars from different sources
- **Prefab Migration**: Migrates PhysBones, PhysBone Colliders, Rotation Constraints, Blendshapes, Materials from original avatars
- **Blender Integration**: Uses Blender for precise mesh merging and bone alignment

## Prerequisites

- **Unity 2022.3 LTS or later**
- **Blender 3.0+** installed and available on system PATH
- **VRChat SDK3** (for VRC components)
- **VRC Dynamics** package

## Usage

### Basic Workflow

1. **Open the Tool**: Go to `Tools > HeadSwapper` in Unity
2. **Prepare Your Avatars**:
   - Delete/hide unwanted meshes (e.g., remove head from body avatar, remove body from head avatar)
   - Position the head avatar where you want it in the final merged result
3. **Select Avatars**: Drag and drop your head and body avatar prefabs into the respective fields
4. **Verify Blender Path**: The tool will auto-detect Blender, or you can manually browse to the executable
5. **Execute**: Click "Execute Head Swap" to begin the merging process
6. **Validate**: Check that PhysBones, constraints, and colliders were migrated correctly

### Detailed Steps

1. **Mesh Preparation**: In your scene, **hide** or **delete** meshes you don't want in the final avatar
2. **Positioning**: Move the head avatar to align with the body avatar's neck/head position
3. **Component Migration**: The tool automatically copies:
   - VRC Avatar Descriptor settings
   - Materials and textures
   - PhysBones and their colliders
   - Rotation constraints
   - Blendshape weights
   - Bounding boxes and light probe anchors

## Troubleshooting

### Common Issues

1. **Blender Not Found**: Ensure Blender is installed and in your system PATH, or manually browse to the executable
2. **Missing VRC Components**: Verify you have the VRChat SDK3 and VRC Dynamics packages imported
3. **Mesh Alignment Issues**: Check that the head avatar is properly positioned relative to the body before execution
4. **Component Migration Failures**: Some complex armature setups may require manual cleanup after merging
