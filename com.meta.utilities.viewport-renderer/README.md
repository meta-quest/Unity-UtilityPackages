# Viewport Renderer

## Intro

This package provides functionality to efficiently render a stencilled view of the game world. This acts as a portal, drawing a different camera over a stencilled region of the screen.

## How to use

1. Create an instance of the ViewportRenderer MonoBehaviour.
2. Create a camera to be rendered into the viewport, configure it as the LensCamera, and disable the Camera component.
3. Create a mesh to act as the lens, assign it the EyeStencilWrite material, and disable the MeshRenderer component.

## How it works

First a lens mesh is drawn, writing a stencil bit to mark pixels that the camera should draw into. This ensures the world is only visible through the mesh, and any other objects obscuring the view will properly obscure the camera.

A screen rect is then calculated for each eye based on the bounds of the lens mesh. Because of the separation of eyes, the screen-space bounds of the lens can be significantly different for each eye. These rects are combined to construct a screen-space viewport containing the lens for both eyes and used to adjust the projection matrix of each eye. The projection matrix is also optionally skewed by the relative camera orientation multiplied by a “trackingFactor”.

Fog density is adjusted by overwriting the FogParams vector, and control is passed to the base DrawObjectsPass to render the game scene, before restoring everything to its initial state.


## Components details

### Viewport Renderer
| Field | Description |
| ----- | ----- |
| Lens Camera | The camera to draw into the stencilled area |
| Lens Mesh | The mesh used to define the stencilled area and camera screen bounds |
| Lens Overlay | A mesh to be composited over the rendered camera |
| Fog Density Factor | A factor to apply to fog density to reduce or increase the fog density |
| Tracking Factor | How much the relative orientation of the camera should be preserved when calculating an adjusted projection matrix |
