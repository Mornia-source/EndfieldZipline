using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace EndfieldZipline.Common.Configs
{
	public class EndfieldZiplineConfig : ModConfig
	{
		public enum ZiplineMoveSpeedPreset
		{
			Default,
			Fast,
			UltraFast,
			Teleport
		}

		public static readonly Color DefaultZiplineRopeColor = new Color(160, 210, 255);

		public override ConfigScope Mode => ConfigScope.ClientSide;

		[Header("ZiplineSettings")]
		[DefaultValue("E")]
		public string ZiplineQuickMoveKey { get; set; }

		[DefaultValue(true)]
		public bool ShowZiplineIconsOnFullscreenMap { get; set; }

		[DefaultValue(false)]
		public bool ZiplineRopePhysicsEffect { get; set; }

		[DefaultValue(false)]
		public bool DisableZiplineSounds { get; set; }

		[DefaultValue(typeof(Color), "160, 210, 255, 255")]
		public Color ZiplineRopeColor { get; set; } = DefaultZiplineRopeColor;

		[JsonIgnore]
		public bool ResetZiplineRopeColorToDefault { get; set; }

		[Header("SimpleMode")]
		[DefaultValue(false)]
		public bool SimpleModeEnabled { get; set; }

		[DefaultValue(ZiplineMoveSpeedPreset.Default)]
		public ZiplineMoveSpeedPreset ZiplineMoveSpeed { get; set; }

		public override void OnLoaded()
		{
			if (ZiplineRopeColor == default)
				ZiplineRopeColor = DefaultZiplineRopeColor;
		}

		public override void OnChanged()
		{
			if (ZiplineRopeColor == default)
				ZiplineRopeColor = DefaultZiplineRopeColor;

			if (ResetZiplineRopeColorToDefault)
			{
				ZiplineRopeColor = DefaultZiplineRopeColor;
				ResetZiplineRopeColorToDefault = false;
			}
		}
	}
}
