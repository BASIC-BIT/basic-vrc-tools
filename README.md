# BASIC VRC Tools

Unity editor tools for BASIC's VRChat avatar creation workflows, distributed as a VPM package.

## Package

- Package id: `net.basicbit.vrc-tools`
- Display name: `BASIC VRC Tools`
- Unity target: `2022.3`
- Menu path: `BASIC/BASIC VRC Tools/Fix Bones on AVI`

## First Tool

`Fix Bones on AVI` is for outfits fit in Blender against a base FBX when the actual Unity avatar has unpacked or overridden body bone positions.

It copies matching avatar-armature bone `localPosition` values onto outfit/merge bones, records Undo, and logs the moved bones.

Discovery paths:

- Modular Avatar `MA Merge Armature` through its public `GetBonesMapping()` API.
- VRCFury Armature Link components when the link target is an explicit object.
- Generic selected-avatar fallback using skinned mesh renderer bones outside the avatar's main humanoid armature.

## Publishing

This repo uses the official VRChat VPM package template workflows.

1. Set the repository variable `PACKAGE_NAME` to `net.basicbit.vrc-tools`.
2. Configure GitHub Pages to deploy from GitHub Actions.
3. Run the `Build Release` workflow.
4. The `Build Repo Listing` workflow publishes the VPM listing site.

Planned public listing URL: `https://vpm.basicbit.net/index.json`.
