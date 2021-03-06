using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SplashScreen;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace SchoolManager
{
    public partial class Settingsforms : Form
    {
        public int Download
        {
            get { return _Download; }
            set
            {
                if (100 > value && value > 0)
                {
                    progresslabel.Text = bytesreceived.ToString("0.###") + "/" + Bytesneeded.ToString("0.###") + " MB";
                    progressBar1.Visible = true;
                }
                else if (progressBar1.Visible)
                {
                    progressBar1.Visible = false;
                    if (value==100)
                        progresslabel.Text = "Downloaded";
                }

                _Download = value;
                progressBar1.Value = value;
            }
        }

        public decimal bytesreceived;
        public decimal Bytesneeded;

        private int _Download;
        private const int EM_SETCUEBANNER = 0x1501;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)]string lParam);
        public Settingsforms()
        {
            InitializeComponent();
            label2.Text += Program.APP_VERSION;
        }

        private void Settingsforms_Deactivate(object sender, EventArgs e)
        {
            Hide();
        }

        private void Loginbutton_Click(object sender, EventArgs e)
        {
            try
            {
                Errormsg.Text = "Attempting fetch...";
                Errormsg.Update();
                MyWebClient web = new MyWebClient();
                web.Proxy = null;
                CredentialCache myCache = new CredentialCache();
                myCache.Add(new Uri("https://intranet.trinity.vic.edu.au/timetable/default.asp"), "NTLM", new NetworkCredential(Userbox.Text, Passbox.Text));
                web.Credentials = myCache;
                String html = web.DownloadString("https://intranet.trinity.vic.edu.au/timetable/default.asp");
                Match match = Regex.Match(html, "<input type=\"hidden\" value=\"(.*?)\" id=\"callType\">", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    Program.Calltype = match.Groups[1].Value;
                }
                match = Regex.Match(html, "<input type=\"hidden\" value=\"(.*?)\" id=\"curDay\">", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out var tempint);
                    Program.SettingsData.Referencedayone = Program.CalDayone(tempint);
                    Program.curDay = tempint;
                }
                match = Regex.Match(html, "<input type=\"hidden\" value=\"(.*?)\" id=\"synID\">", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    Program.SynID = Convert.ToInt32(match.Groups[1].Value);
                }
                match = Regex.Match(html, "value=\"(.*?)\" id=\"curTerm\"", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    Program.SettingsData.Curterm = Convert.ToInt32(match.Groups[1].Value);
                }
                string sqlquery = "";
                if (Program.Calltype == "student")
                    sqlquery =
                        "%20AND%20TD.PeriodNumber%20>=%200%20AND%20TD.PeriodNumberSeq%20=%201AND%20(stopdate%20IS%20NULL%20OR%20stopdate%20>%20getdate())--";
                if (Program.SettingsData.Curterm == 0)
                {
                    for (int i = 4; i > 0; i--)
                    {
                        myCache.Add(new Uri("https://intranet.trinity.vic.edu.au/timetable/getTimetable1.asp?synID=" + Program.SynID + "&year=" + DateTime.Now.Year + "&term=" + i + sqlquery + "&callType=" + Program.Calltype), "NTLM", new NetworkCredential(Userbox.Text, Passbox.Text));
                        web.Credentials = myCache;
                        html = web.DownloadString("https://intranet.trinity.vic.edu.au/timetable/getTimetable1.asp?synID=" + Program.SynID + "&year=" + DateTime.Now.Year + "&term=" + i + sqlquery + "&callType=" + Program.Calltype);
                        if (html.Length > 10)
                        {
                            Program.SettingsData.Curterm = i;
                            break;
                        }
                    }
                }
                else
                {
                    myCache.Add(new Uri("https://intranet.trinity.vic.edu.au/timetable/getTimetable1.asp?synID=" + Program.SynID + "&year=" + DateTime.Now.Year + "&term=" + Program.SettingsData.Curterm + sqlquery + "&callType=" + Program.Calltype), "NTLM", new NetworkCredential(Userbox.Text, Passbox.Text));
                    web.Credentials = myCache;
                    html = web.DownloadString("https://intranet.trinity.vic.edu.au/timetable/getTimetable1.asp?synID=" + Program.SynID + "&year=" + DateTime.Now.Year + "&term=" + Program.SettingsData.Curterm + sqlquery + "&callType=" + Program.Calltype);
                }
                List<period> timetableList = JsonConvert.DeserializeObject<List<period>>(html);
                using (StreamWriter file = File.CreateText(Program.CURRENT_DIRECTORY + "/Timetable.json"))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(file, timetableList);
                }
                Int16 colorint = 0;
                Program.TimetableList.Clear();
                foreach (var V in timetableList)
                { 
                    if (!Program.ColorRef.ContainsKey(V.ClassCode))
                    {
                        Program.ColorRef.Add(V.ClassCode,Program.ColourTable[colorint]);
                        colorint++;
                        if (colorint >= Program.ColourTable.Count)
                            colorint = 0;
                    }
                    Program.TimetableList.Add(V.DayNumber.ToString() + V.PeriodNumber, V);
                }
                web.Dispose();
                Errormsg.Text = "Successfully extracted! ";
                Errormsg.Update();
                    TcpClient tcpclnt = new TcpClient();
                try
                {
                    if (tcpclnt.ConnectAsync("timetable.duckdns.org", 80).Wait(1200))
                    {
                        String str = "T" + Program.SynID + " " + Program.Calltype + " " + Program.APP_VERSION;
                        Stream stm = tcpclnt.GetStream();
                        ASCIIEncoding asen = new ASCIIEncoding();
                        byte[] ba = asen.GetBytes(str);
                        stm.Write(ba, 0, ba.Length);
                        Errormsg.Text += "Saved";
                    }
                    else
                        Errormsg.Text += "Filed";
                }
                catch
                {
                    Errormsg.Text += "Filed";
                }

                tcpclnt.Close();
            }
            catch (WebException ee)
            {
                Errormsg.Text = ee.Message;
                if (ee.Message.Contains("Unauthorized"))
                {
                    Errormsg.Text = "Authorization failed";
                }
            }
            catch (Exception ee)
            {
                Errormsg.Text = ee.Message;
                MessageBox.Show(ee.ToString());
            }

            Userbox.Text = "";
            Passbox.Text = "";
        }

        private void Settingsforms_Shown(object sender, EventArgs e)
        {
            numericUpDown1.Value = Program.SettingsData.TimeOffset;
            SendMessage(Userbox.Handle, EM_SETCUEBANNER, 0, "Username");
            SendMessage(Passbox.Handle, EM_SETCUEBANNER, 0, "Password");
        }

        private void Passbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                Loginbutton.PerformClick();
        }

        private void Settingsforms_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            e.Cancel = (e.CloseReason == CloseReason.UserClosing);  // this cancels the close event.
        }

        private void Weekoverride_Click(object sender, EventArgs e)
        {
            if (Program.SettingsData.Dayoffset == 0)
            {
                Program.SettingsData.Dayoffset = 7;
            }
            else
            {
                Program.SettingsData.Dayoffset = 0;
            }

            Program.curDay = Program.Fetchday();
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            Program.SettingsData.TimeOffset = int.Parse(numericUpDown1.Value.ToString());
        }
    }
}
