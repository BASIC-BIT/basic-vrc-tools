# BASIC VRC Tools

BASIC's VRChat creator toolchain.

## Armature Pose Fixer

Use `BASIC/BASIC VRC Tools/Fix Bones on AVI` with an avatar root or avatar child selected.

The tool aligns matching outfit/merge bones to the avatar armature's current local bone positions. This is intended for outfits fit in Blender against a base FBX when the in-Unity avatar has unpacked/overridden body bone positions.

Current discovery paths:

- Modular Avatar `MA Merge Armature` via its public `GetBonesMapping()` API.
- VRCFury Armature Link components when the link target is an explicit object.
- Generic selected-avatar fallback using skinned mesh renderer bones outside the avatar's main humanoid armature.

The tool records Undo and logs how many bones were moved.
