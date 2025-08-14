import sys
import os

# Ensure we can import from the same directory
script_dir = os.path.dirname(__file__)
sys.path.append(script_dir)

print(">>> MAIN.PY STARTED")

import loader
import processor

if "--" in sys.argv:
    script_args = sys.argv[sys.argv.index("--") + 1:]
    
    print(">>> DEBUG: All sys.argv:", sys.argv)
    print(">>> DEBUG: script_args:", script_args)
    print(">>> DEBUG: Length of script_args:", len(script_args))
    
    if len(script_args) >= 3:
        source_fbx, target_fbx, json_path = script_args[:3]
    else:
        print("Usage: blender file.blend --python script.py -- <json_path> <source_fbx> <target_fbx>")
        sys.exit(1)
else:
    print("Usage: blender file.blend --python script.py -- <json_path> <source_fbx> <target_fbx>")
    sys.exit(1)

print(">>> JSON PATH:", json_path)
print(">>> SOURCE FBX:", source_fbx)
print(">>> TARGET FBX:", target_fbx)

json_data = loader.load(json_path)
source_obj = loader.import_fbx(source_fbx);
target_obj = loader.import_fbx(target_fbx);

print(f"Head FBX imported at: {source_obj.location}")
print(f"Body FBX imported at: {target_obj.location}")

print(">>> JSON LOADED:", json_data)

processor.apply(source_obj, target_obj, json_data)

print(">>> MAIN.PY FINISHED")


# blender --python PythonFiles/main.py -- "fbx1" "fbx2" "Temp/transform.json"
