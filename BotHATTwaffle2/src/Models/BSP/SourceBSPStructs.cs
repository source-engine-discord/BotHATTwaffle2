namespace bsp_pakfile
{
    public static class SourceBSPStructs
    {
        public static class Constants
        {
            public const int HEADER_LUMPS = 64;
            public const int IDBSPHEADER = 0x50534256;      // (('P'<<24)+('S'<<16)+('B'<<8)+'V') // little-endian "VBSP"

            public const int MAX_BRUSH_SIDES = 128;     // on a single brush

            public const int OVERLAY_BSP_FACE_COUNT = 64;


            // upper design bounds
            public const int MIN_MAP_DISP_POWER = 2;    // Minimum and maximum power a displacement can be.
            public const int MAX_MAP_DISP_POWER = 4;

            public const int MAX_DISP_CORNER_NEIGHBORS = 4;

            public static int NUM_DISP_POWER_VERTS(int power) => (((1 << (power)) + 1) * ((1 << (power)) + 1));
            public static int NUM_DISP_POWER_TRIS(int power) => ((1 << (power)) * (1 << (power)) * 2);

            public const int MAX_MAP_MODELS = 1024;
            public const int MAX_MAP_BRUSHES = 8192;
            public const int MAX_MAP_ENTITIES = 8192;
            public const int MAX_MAP_TEXINFO = 12288;
            public const int MAX_MAP_TEXDATA = 2048;
            public const int MAX_MAP_DISPINFO = 2048;
            public const int MAX_MAP_DISP_VERTS = (MAX_MAP_DISPINFO * ((1 << MAX_MAP_DISP_POWER) + 1) * ((1 << MAX_MAP_DISP_POWER) + 1));
            public const int MAX_MAP_DISP_TRIS = ((1 << MAX_MAP_DISP_POWER) * (1 << MAX_MAP_DISP_POWER) * 2);
            public static int MAX_DISPVERTS = NUM_DISP_POWER_VERTS(MAX_MAP_DISP_POWER);
            public static int MAX_DISPTRIS = NUM_DISP_POWER_TRIS(MAX_MAP_DISP_POWER);
            public const int MAX_MAP_AREAS = 256;
            public const int MAX_MAP_AREA_BYTES = (MAX_MAP_AREAS / 8);
            public const int MAX_MAP_AREAPORTALS = 1024;

            // Planes come in pairs, thus an even number.
            public const int MAX_MAP_PLANES = 65536;
            public const int MAX_MAP_NODES = 65536;
            public const int MAX_MAP_BRUSHSIDES = 65536;
            public const int MAX_MAP_LEAFS = 65536;
            public const int MAX_MAP_VERTS = 65536;
            public const int MAX_MAP_VERTNORMALS = 256000;
            public const int MAX_MAP_VERTNORMALINDICES = 256000;
            public const int MAX_MAP_FACES = 65536;
            public const int MAX_MAP_LEAFFACES = 65536;
            public const int MAX_MAP_LEAFBRUSHES = 65536;
            public const int MAX_MAP_PORTALS = 65536;
            public const int MAX_MAP_CLUSTERS = 65536;
            public const int MAX_MAP_LEAFWATERDATA = 32768;
            public const int MAX_MAP_PORTALVERTS = 128000;
            public const int MAX_MAP_EDGES = 256000;
            public const int MAX_MAP_SURFEDGES = 512000;
            public const int MAX_MAP_LIGHTING = 0x1000000;
            public const int MAX_MAP_VISIBILITY = 0x1000000; // increased BSPVERSION 7
            public const int MAX_MAP_TEXTURES = 1024;
            public const int MAX_MAP_WORLDLIGHTS = 8192;
            public const int MAX_MAP_CUBEMAPSAMPLES = 1024;
            public const int MAX_MAP_OVERLAYS = 512;
            public const int MAX_MAP_WATEROVERLAYS = 16384;
            public const int MAX_MAP_TEXDATA_STRING_DATA = 256000;
            public const int MAX_MAP_TEXDATA_STRING_TABLE = 65536;

            // this is stuff for trilist/tristrips, etc.
            public const int MAX_MAP_PRIMITIVES = 32768;
            public const int MAX_MAP_PRIMVERTS = 65536;
            public const int MAX_MAP_PRIMINDICES = 65536;

            public const int WATEROVERLAY_BSP_FACE_COUNT = 256;
            public const int WATEROVERLAY_RENDER_ORDER_NUM_BITS = 2;
            public const int WATEROVERLAY_NUM_RENDER_ORDERS = (1 << WATEROVERLAY_RENDER_ORDER_NUM_BITS);
            public const int WATEROVERLAY_RENDER_ORDER_MASK = 0xC000;// top 2 bits set
        }

        public enum Lumps
        {
            //** - this lump is splitted for different versions
            LUMP_ENTITIES = 0,        // Map entities; ASCII
            LUMP_PLANES = 1,        // Plane array
            LUMP_TEXDATA = 2,        // Index to texture names
            LUMP_VERTEXES = 3,        // Vertex array
            LUMP_VISIBILITY = 4,        // Compressed visibility bit arrays
            LUMP_NODES = 5,        // BSP tree nodes
            LUMP_TEXINFO = 6,        // Face texture array
            LUMP_FACES = 7,        // Face array
            LUMP_LIGHTING = 8,        // Lightmap samples
            LUMP_OCCLUSION = 9,        // Occlusion polygons and vertices
            LUMP_LEAFS = 10,       // BSP tree leaf nodes
            LUMP_FACEIDS = 11,       // Correlates between dfaces and Hammer face IDs. Also used as random seed for 'detail prop' placement
            LUMP_EDGES = 12,       // Edge array
            LUMP_SURFEDGES = 13,       // Index of edges
            LUMP_MODELS = 14,       // Brush models (geometry of brush entities)
            LUMP_WORLDLIGHTS = 15,       // Internal world lights converted from the entity lump
            LUMP_LEAFFACES = 16,       // Index to faces in each leaf
            LUMP_LEAFBRUSHES = 17,       // Index to brushes in each leaf
            LUMP_BRUSHES = 18,       // Brush array
            LUMP_BRUSHSIDES = 19,       // Brushside array
            LUMP_AREAS = 20,       // Area array
            LUMP_AREAPORTALS = 21,       // Portals between area
            LUMP_PROPCOLLISION = 22, // ** // Static props convex hull lists
            LUMP_PROPHULLS = 23, // ** // Static props convex hulls
            LUMP_PROPHULLVERTS = 24, // ** // Static prop collision vertices
            LUMP_PROPTRIS = 25, // ** // Static prop per hull triangle index start/count
            LUMP_DISPINFO = 26,       // Displacement surface array
            LUMP_ORIGINALFACES = 27,       // Brush faces array before splitting
            LUMP_PHYSDISP = 28,       // Displacement physics collision data
            LUMP_PHYSCOLLIDE = 29,       // Physics collision data
            LUMP_VERTNORMALS = 30,       // Face plane normals
            LUMP_VERTNORMALINDICES = 31,       // Face plane normal index array
            LUMP_DISP_LIGHTMAP_ALPHAS = 32,       // Displacement lightmap alphas (unused/empty since Source 2006)
            LUMP_DISP_VERTS = 33,       // Vertices of displacement surface meshes
            LUMP_DISP_LIGHTMAP_SAMPLE_POSITIONS = 34,       // Displacement lightmap sample positions
            LUMP_GAME_LUMP = 35,       // Game-specific data lump
            LUMP_LEAFWATERDATA = 36,       // Data for leaf nodes that are inside water
            LUMP_PRIMITIVES = 37,       // Water polygon data
            LUMP_PRIMVERTS = 38,       // Water polygon vertices
            LUMP_PRIMINDICES = 39,       // Water polygon vertex index array
            LUMP_PAKFILE = 40,       // Embedded uncompressed Zip-format file
            LUMP_CLIPPORTALVERTS = 41,       // Clipped portal polygon vertices
            LUMP_CUBEMAPS = 42,       // ent_cubemap location array
            LUMP_TEXDATA_STRING_DATA = 43,       // Texture name data
            LUMP_TEXDATA_STRING_TABLE = 44,       // Index array into texdata string data
            LUMP_OVERLAYS = 45,       // info_overlay data array
            LUMP_LEAFMINDISTTOWATER = 46,       // Distance from leaves to water
            LUMP_FACE_MACRO_TEXTURE_INFO = 47,       // Macro texture info for faces
            LUMP_DISP_TRIS = 48,       // Displacement surface triangles
            LUMP_PROP_BLOB = 49, // ** // Static prop triangle and string data
            LUMP_WATEROVERLAYS = 50,       //   Confirm:  info_overlay's on water faces?
            LUMP_LEAF_AMBIENT_INDEX_HDR = 51, // ** // Index of LUMP_LEAF_AMBIENT_LIGHTING_HDR
            LUMP_LEAF_AMBIENT_INDEX = 52, // ** // Index of LUMP_LEAF_AMBIENT_LIGHTING
            LUMP_LIGHTING_HDR = 53,       // HDR lightmap samples
            LUMP_WORLDLIGHTS_HDR = 54,       // Internal HDR world lights converted from the entity lump
            LUMP_LEAF_AMBIENT_LIGHTING_HDR = 55,       // Per-leaf ambient light samples (HDR)
            LUMP_LEAF_AMBIENT_LIGHTING = 56,       // Per-leaf ambient light samples (LDR)
            LUMP_XZIPPAKFILE = 57,       // XZip version of pak file for Xbox. Deprecated.
            LUMP_FACES_HDR = 58,       // HDR maps may have differen face data
            LUMP_MAP_FLAGS = 59,       // Extended level-wide flags. Not present in all levels.
            LUMP_OVERLAY_FADES = 60,       // Fade distances for overlays
            LUMP_OVERLAY_SYSTEM_LEVELS = 61,       // System level settings (min/max CPU & GPU to render this overlay)
            LUMP_PHYSLEVEL = 62,       // ??
            LUMP_DISP_MULTIBLEND = 63        // Displacement multiblend info
        }

        public struct dheader_t
        {
            public int ident;                               //BSP file identifier
            public int version;                             //BSP file version
            public lump_t[] lumps; // [HEADER_LUMPS]        //lump directory array
            public int mapRevision;                         //the map's revision (iteration, version) number
        }

        public struct lump_t
        {
            public int fileofs;                             // offset into file (bytes)
            public int filelen;                             // length of lump (bytes)
            public int version;                             // lump format version
            public char[] fourCC; // [4]                    // lump ident code
        }
    }
}
