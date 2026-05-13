Shader "Custom/MediaPipeStyle" {
    Properties {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        ZTest Always // <--- Forza il pallino davanti a tutto
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            SetTexture [_MainTex] { combine texture * primary }
        }
    }
}