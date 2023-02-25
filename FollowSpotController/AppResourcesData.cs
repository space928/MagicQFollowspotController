using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;

namespace MidiApp
{
    [Serializable]
    public struct AppResourcesData
    {
        public const int FILE_FORMAT_VERSION = 9;

        public int fileFormatVersion = FILE_FORMAT_VERSION;
        public TheatrePhysicalData theatrePhysicalData;
        public LightingBarPhysicalData[] lightingBars;
        public BoxPhysicalData[] boxes;
        public LightDefinition[] lights;
        public NetworkSettings network;
        public CameraPosition[] cameraPositions;
        public Marker[] markers;
        public FixtureProfile[] fixtureProfiles;

        /// <summary>
        /// Json Serialization settings to be used to serialze/deserialize AppResources.
        /// </summary>
        public static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            IgnoreReadOnlyFields = false,
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        public AppResourcesData()
        {
            fileFormatVersion = FILE_FORMAT_VERSION;
            lightingBars = new LightingBarPhysicalData[1];
            boxes = new BoxPhysicalData[0];
            lights = new LightDefinition[1];
            cameraPositions = new CameraPosition[1];
            markers = new Marker[0];
            fixtureProfiles = new FixtureProfile[1];
        }
    }

    [Serializable]
    public struct TheatrePhysicalData
    {
        public float width;
        public float roof;

        public float stairHeight;
        public float stairStart;
        public float stairLength;

        public float stageHeight;
        public float stageStart;
        public float stageWidth;
        public float stageDepth;
        public float stageCurveDepth;
    }

    [Serializable]
    public struct LightingBarPhysicalData
    {
        public float height;
        public float width;
        public float offset;
    }

    [Serializable]
    public struct BoxPhysicalData
    {
        public Point3D location;
        public Point3D dimensions;
        public Point3D rotation;
    }

    [Serializable]
    public struct LightDefinition
    {
        public string fixture;
        public int head;
        public byte universe;
        public byte address;
        public int bar;
        public float xOffset;
    }

    [Serializable]
    public struct NetworkSettings
    {
        public required string magicQIP;
        public required ArtNetSettings artNet;
    }

    [Serializable]
    public struct ArtNetSettings
    {
        public string rxIP;
        public string rxSubNetMask;
        public string txIP;
        public string txSubNetMask;
        public bool broadcast;
        public byte universe;
    }

    [Serializable]
    public struct CameraPosition
    {
        public Point3D location;
        public Point3D direction;
        public Point3D upDirection;
        public double fov;
    }

    [Serializable]
    public struct Marker
    {
        public Point3D position;
        public int clientID;
        public int markerID;
    }

    [Serializable]
    public struct FixtureProfile
    {
        public string name;
        public int panLowChannel;
        public int panHighChannel;
        public int tiltLowChannel;
        public int tiltHighChannel;
        public int zoomChannel;
        public int irisChannel;

        public bool position16bit;
        public bool zoomControl;
        public bool irisControl;
        public bool panInvert;
        public bool tiltInvert;
        public bool panTiltSwap;

        public float panRange;
        public float tiltRange;
        public float zoomMin;
        public float zoomMax;
        public float irisMin;
        public float irisMax;
    }
}
