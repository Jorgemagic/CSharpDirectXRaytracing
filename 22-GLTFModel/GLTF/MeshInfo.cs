// Copyright © Wave Engine S.L. All rights reserved. Use is subject to license terms.

using glTFLoader.Schema;

namespace RayTracingTutorial22.GLTF
{
    public class MeshInfo
    {
        public BufferView IndicesBufferView;
        public BufferView[] AttributeBufferView;

        public MeshInfo(BufferView indices, BufferView[] attributes)
        {
            this.IndicesBufferView = indices;
            this.AttributeBufferView = attributes;
        }
    }
}
