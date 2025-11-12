namespace Renderer;

[System.Flags]
public enum CameraLayer 
{
	Main = Camera0,
	MainCamera = Main,

	UI = Camera1,
	UICamera = UI,

	UISample = Camera2,
	UISampleCamera = UISample,

	Camera0 = 1 << 0,
	Camera1 = 1 << 1,
	Camera2 = 1 << 2,
	Camera3 = 1 << 3,
	Camera4 = 1 << 4,
	Camera5 = 1 << 5,
	Camera6 = 1 << 6,
	Camera7 = 1 << 7,
	Camera8 = 1 << 8,
	Camera9 = 1 << 9,
	Camera10 = 1 << 10,
	Camera11 = 1 << 11,
	Camera12 = 1 << 12,
	Camera13 = 1 << 13,
	Camera14 = 1 << 14,
	Camera15 = 1 << 15,
	Camera16 = 1 << 16,
	Camera17 = 1 << 17,
	Camera18 = 1 << 18,
	Camera19 = 1 << 19,
	Camera20 = 1 << 20,
	Camera21 = 1 << 21,
	Camera22 = 1 << 22,
	Camera23 = 1 << 23,
	Camera24 = 1 << 24,
	Camera25 = 1 << 25,
	Camera26 = 1 << 26,
	Camera27 = 1 << 27,
	Camera28 = 1 << 28,
	Camera29 = 1 << 29,
	Camera30 = 1 << 30,
	Camera31 = 1 << 31,
}
