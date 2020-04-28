# CSharp DirectX Raytracing Tutorials
This repository contain tutorials demostrating how to use DirectX12 Raytracing with CSharp. The Nvidia original C++ tutorials can be found [here](https://github.com/NVIDIAGameWorks/DxrTutorials). The DirectX12 CSharp binding used was [Vortice](https://github.com/amerkoleci/Vortice.Windows).

## Requirements:

- A GPU that supports DXR (Such as NVIDIA's Volta or Turing hardware)
- Windows 10 RS5 (version 1809)
- [Windows 10 SDK version 1809 (10.0.17763.0)](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive)
- Visual Studio 2019

## Tutorials

### [Tutorial 01 Create Window](01-CreateWindow/)

![alt Create Window](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/CreateWindow.png)

### [Tutorial 02 Initialize DXR](02-InitDXR/)

![alt Initialize DXR](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/InitializeDXR.png)

### [Tutorial 03 Acceleration Structure](03-AccelerationStructure/)

Nothing to show

### [Tutorial 04 Raytracing PipelineState](04-RtPipelineState/)

Nothing to show

### [Tutorial 05 Shader Table](05-ShaderTable/)

Nothing to show

### [Tutorial 06 Raytrace](06-Raytrace/)

Nothing to show

### [Tutorial 07 Basic Shaders](07-BasicShaders/)

![alt Draw triangle](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/DrawTriangle.png)

### [Tutorial 08 Instancing](08-Instancing/)

![alt Instancing](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/Instancing.png)

### [Tutorial 09 Constant Buffer](09-ConstantBuffer/)

![alt Color Constant Buffer](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/ConstantBuffer.png)

### [Tutorial 10 Per Instance Constant Buffer](10-PerInstanceConstantBuffer/)

![alt Individual Constant Buffer](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/ConstantBuffers.png)

### [Tutorial 11 Second Geometry](11-SecondGeometry/)

![alt Add plane geometry](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/Plane.png)

### [Tutorial 12 Per Geometry Hit Shader](12-PerGeometryHitShader/)

![alt Triangle and Plane HitShaders](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/TriangleAndPlaneHitShader.png)

### [Tutorial 13 Second Ray Type](13-SecondRayType/)

![alt Simple Shadow](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/Shadow.png)

### [Tutorial 14 Refit](14-Refit/)

![alt Rotate triangles](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/UpdateGeometryTransform.png)

## Extra Tutorials

After I ported Raytracing DXR Nvidia tutorials to CSharp I think that would be a great idea to extend theses tutorials with some more. So I am going to add new extra raytracing tutorials to explain how to create more complex raytracing scenes.

### [Tutorial 15 Primitives](15-Primitives/)

How to create a Raytracing Acceleration Structure from vertex and index geometry buffers.
![alt Primitives](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/Primitives.png)

### [Tutorial 16 Lighting](16-Lighting/)

How to lighting mesh using Raytracing pipeline. The acceleration Structures only have information about the vertex position of the mesh so we need to pass vertexBuffer and indexBuffer information to the shader to reconstruct the vertex information after a hit.
![alt Primitives](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/Lighting.png)

### [Tutorial 17 Shadow](17-Shadow/)

How to project shadows using Raytracing pipeline. In this tutorial, we are going to add a second geometry (ground) to the Acceleration Structure and throw a second ray to know whether a hit point is in shadow.
![alt Primitives](https://github.com/Jorgemagic/CSharpDirectXRaytracing/blob/master/Screenshots/Shadow01.png)