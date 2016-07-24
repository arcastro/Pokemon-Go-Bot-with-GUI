using AllEnum;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PokemonGo.RocketAPI.GUI
{
    public partial class Form1 : Form
    {
        public static Form1 gameForm;

        GMapOverlay markersOverlay;

        TimeSpan ts = new TimeSpan();

        public int ExpGained = 0;
        public int StarDust = 0;

        bool LoadingSettings = false;

        GMarkerGoogle playerMarker;

        Task mainTask;
        CancellationTokenSource mainTaskCancel;

        public double Lat = UserSettings.Default.DefaultLatitude, Lng = UserSettings.Default.DefaultLongitude;

        Dictionary<string, GMapMarker> mapMarkers = new Dictionary<string, GMapMarker>();

        private void Form1_Load(object sender, EventArgs e)
        {
            gameForm = this;

            LoadingSettings = true;
            var startSettings = new Settings();

            comboBox1.DataSource = Enum.GetValues(typeof(AuthType));           

            comboBox1.SelectedItem = startSettings.AuthType;

            tbLat.Text = startSettings.DefaultLatitude.ToString();
            tbLng.Text = startSettings.DefaultLongitude.ToString();

            tbLogin.Text = startSettings.PtcUsername.ToString();
            tbPswd.Text = startSettings.PtcPassword.ToString();

            LoadingSettings = false;

            gMapControl1.Bearing = 0;

            gMapControl1.CanDragMap = true;

            gMapControl1.DragButton = MouseButtons.Left;

            gMapControl1.GrayScaleMode = true;

            gMapControl1.MarkersEnabled = true;

            gMapControl1.MaxZoom = 18;

            gMapControl1.MinZoom = 2;

            gMapControl1.MouseWheelZoomType = MouseWheelZoomType.MousePositionWithoutCenter;

            gMapControl1.NegativeMode = false;

            gMapControl1.PolygonsEnabled = true;

            gMapControl1.RoutesEnabled = true;

            gMapControl1.ShowTileGridLines = false;

            gMapControl1.Zoom = 18;

            gMapControl1.MapProvider = GMap.NET.MapProviders.GMapProviders.GoogleMap;
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerOnly;

            gMapControl1.Position = new GMap.NET.PointLatLng(Lat, Lng);

            markersOverlay = new GMapOverlay("markers");
            //GMarkerGoogle marker = new GMarkerGoogle(new PointLatLng(Lat, Lng), GMarkerGoogleType.green);
            //markersOverlay.Markers.Add(marker);
            gMapControl1.Overlays.Add(markersOverlay);
        }

        public void PushNewRow(string rowText, Color rowColor)
        {
            Invoke(new Action(() =>
            {
                gameForm?.richTextBox1.AppendText(rowText, rowColor);
            }));
        }


        public void PushNewInfo(string infoType, string info)
        {
            int gained = 0;
            double coord = 0;
            switch (infoType)
            {
                case "profileInfo":
                    Invoke(new Action(() =>
                    {
                        gameForm.charInfo.Text += (gameForm.charInfo.Text.Length == 0 ? "" : "\r\n") + info;
                    }));
                    break;
                case "xpGained":
                    int.TryParse(info, out gained);
                    ExpGained += gained;
                    Invoke(new Action(() =>
                    {
                        gameForm.xpBox.Text = ExpGained.ToString();
                    }));
                    break;
                case "sdGained":
                    int.TryParse(info, out gained);
                    StarDust += int.Parse(info);
                    Invoke(new Action(() =>
                    {
                        gameForm.sdBox.Text = StarDust.ToString();
                    }));
                    break;
                case "wipe":
                    StarDust = 0;
                    ExpGained = 0;
                    Invoke(new Action(() =>
                    {
                        gameForm.charInfo.Text = "";
                        gameForm.sdBox.Text = "0";
                        gameForm.xpBox.Text = "0";
                    }));
                    break;
                case "nextLat":
                    double.TryParse(info, out coord);
                    Lat = coord;
                    break;
                case "nextLng":
                    double.TryParse(info, out coord);
                    Lng = coord;
                    Invoke(new Action(() =>
                    {
                        UpdateCoords();
                    }));
                    break;
            }
        }
        public void PushNewMapObject(string oType, string oName, double lat, double lng, string uid)
        {

            switch (oType)
            {
                case "ps":
                    if (!mapMarkers.ContainsKey(uid))
                    {
                        GMarkerGoogle marker = new GMarkerGoogle(new PointLatLng(lat, lng), oName != "lured" ? Properties.Resources.Pstop : Properties.Resources.PstopLured);
                        marker.ToolTipText = "PokeStop";
                        markersOverlay.Markers.Add(marker);
                        mapMarkers.Add(uid, marker);
                    }
                    break;
                case "pm_rm":
                    if (mapMarkers.ContainsKey(uid))
                    {
                        markersOverlay.Markers.Remove(mapMarkers[uid]);
                    }
                    break;
                case "pm":
                    if (!mapMarkers.ContainsKey(uid))
                    {
                        CreatePokemonMarker(oName, lat, lng, uid);
                    }
                    break;
                default:
                    break;
            }

        }

        public void CreatePokemonMarker(string oName, double lat, double lng, string uid)
        {
            PokemonId pokemon = (PokemonId)Enum.Parse(typeof(PokemonId), oName);

            GMarkerGoogle marker = new GMarkerGoogle(new PointLatLng(lat, lng), pokemon.ToBitmap());
            marker.ToolTipText = oName;
            markersOverlay.Markers.Add(marker);
            mapMarkers.Add(uid, marker);
        }


        public Form1()
        {
            InitializeComponent();

            

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            SuspendLayout();
            var rtb = sender as RichTextBox;
            Point pt = rtb.GetPositionFromCharIndex(rtb.SelectionStart);
            if (pt.Y > rtb.Height)
            {
                rtb.ScrollToCaret();
            }
            ResumeLayout(true);
        }

        private void coordsLinkLb_Click(object sender, EventArgs e)
        {
            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";
            System.Diagnostics.Process.Start($"https://www.google.ru/maps?z=18&q=loc:{Lat.ToString(nfi)},{Lng.ToString(nfi)}");
        }

        void UpdateCoords()
        {
            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";
            coordsLinkLb.Text = $"{Lat.ToString(nfi)}, {Lng.ToString(nfi)}";

            if (playerMarker == null)
            {
                playerMarker = new GMarkerGoogle(new PointLatLng(Lat, Lng), GMarkerGoogleType.arrow);
                markersOverlay.Markers.Add(playerMarker);
            }
            else
            {
                playerMarker.Position = new PointLatLng(Lat, Lng);
                UserSettings.Default.DefaultLatitude = Lat;
                UserSettings.Default.DefaultLongitude = Lng;
            }
            
        }

        private void tbLogin_TextChanged(object sender, EventArgs e)
        {
            UserSettings.Default.PtcUsername = (sender as TextBox).Text;           
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            UserSettings.Default.Save();

            mainTaskCancel?.Cancel();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var cb = (sender as ComboBox);
            if (cb.SelectedIndex > -1 && !LoadingSettings)
            {
                AuthType auth;
                Enum.TryParse((sender as ComboBox).SelectedValue.ToString(), out auth);
                UserSettings.Default.AuthType = auth.ToString();
            }
        }

        private void tbPswd_TextChanged(object sender, EventArgs e)
        {
            UserSettings.Default.PtcPassword = (sender as TextBox).Text;
        }

        private void tbLat_TextChanged(object sender, EventArgs e)
        {
            double newVal = 0;
            if (double.TryParse((sender as TextBox).Text, out newVal))
                UserSettings.Default.DefaultLatitude = newVal;
        }

        private void tbLng_TextChanged(object sender, EventArgs e)
        {
            double newVal = 0;
            if (double.TryParse((sender as TextBox).Text, out newVal))
                UserSettings.Default.DefaultLongitude = newVal;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            (sender as Button).Enabled = false;
            Logger.SetLogger(new FormLogger(LogLevel.Info));
            timer1.Start();
            mainTaskCancel = new CancellationTokenSource();
            var ct = mainTaskCancel.Token;
            mainTask = Task.Run(() =>
            {
                try
                {
                    new Logic.Logic(new Settings()).Execute();
                }
                catch (PtcOfflineException)
                {
                    Logger.Write("PTC Servers are probably down OR your credentials are wrong. Try google", LogLevel.Error);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Unhandled exception: {ex}", LogLevel.Error);
                }
            }, ct);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ts = ts.Add(new TimeSpan(0, 0, 1));
            timerLb.Text = ts.ToString();
        }
    }
}
