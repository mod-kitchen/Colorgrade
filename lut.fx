#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float LUTProgress;

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

Texture2D<float4> LUT1;
sampler2D LUT1Sampler = sampler_state
{
	Texture = <LUT1>;
};

Texture2D<float4> LUT2;
sampler2D LUT2Sampler = sampler_state
{
	Texture = <LUT2>;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
}; 

float4 Lookup(int2 baseTable, int x, int y) {
	float2 coord = (1.0 / 64.0) * float2(baseTable.x + x + 0.5, baseTable.y + y + 0.5);
	return lerp(tex2D(LUT1Sampler, coord), tex2D(LUT2Sampler, coord), LUTProgress);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float4 baseTexture = tex2D(SpriteTextureSampler,input.TextureCoordinates) * input.Color;

	float red = baseTexture.r * 15;
	float redinterpol = frac(red);
	float green = baseTexture.g * 15;
	float greeninterpol = frac(green);
	float blue = baseTexture.b * 15;
	float blueinterpol = frac(blue);
	float row = trunc(blue / 4);
	float col = trunc(blue % 4);
	int2 blueBaseTable = int2(trunc(col * 16), trunc(row * 16));

	float4 b0r1g0;
	float4 b0r0g1;
	float4 b0r1g1;
	float4 b1r0g0;
	float4 b1r1g0;
	float4 b1r0g1;
	float4 b1r1g1;
	float4 b0r0g0 = Lookup(blueBaseTable, red, green);
	if (red < 15)
		b0r1g0 = Lookup(blueBaseTable, red + 1, green);
	else
		b0r1g0 = b0r0g0;
	if (green < 15)
	{
		b0r0g1 = Lookup(blueBaseTable, red, green + 1);
		if (red < 15)
			b0r1g1 = Lookup(blueBaseTable, red + 1, green + 1);
		else
			b0r1g1 = b0r0g1;
	}
	else
	{
		b0r0g1 = b0r0g0;
		b0r1g1 = b0r0g1;
	}
	if (blue < 15)
	{
		blue += 1;
		row = trunc(blue / 4);
		col = trunc(blue % 4);
		blueBaseTable = int2(trunc(col * 16), trunc(row * 16));
		b1r0g0 = Lookup(blueBaseTable, red, green);
		if (red < 15)
			b1r1g0 = Lookup(blueBaseTable, red + 1, green);
		else
			b1r1g0 = b0r0g0;
		if (green < 15)
		{
			b1r0g1 = Lookup(blueBaseTable, red, green + 1);
			if (red < 15)
				b1r1g1 = Lookup(blueBaseTable, red + 1, green + 1);
			else
				b1r1g1 = b0r0g1;
		}
		else
		{
			b1r0g1 = b0r0g0;
			b1r1g1 = b0r0g1;
		}
	}
	else
	{
		b1r0g0 = b0r0g0;
		b1r1g0 = b0r1g0;
		b1r0g1 = b0r0g0;
		b1r1g1 = b0r1g1;
	}
	float4 result = lerp(lerp(b0r0g0, b0r1g0, redinterpol), lerp(b0r0g1, b0r1g1, redinterpol), greeninterpol);
	float4 result2 = lerp(lerp(b1r0g0, b1r1g0, redinterpol), lerp(b1r0g1, b1r1g1, redinterpol), greeninterpol);
	result = lerp(result, result2, blueinterpol);

	return result; //baseTexture works here
}

technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};