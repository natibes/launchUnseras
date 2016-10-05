using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Net;
using System.Diagnostics;
namespace MGPatcher
{
    public partial class PatcherWindow : Form
    {
        bool direct_update = false;
        string game_name = "test";
        string current_version = "1.0.0.0";
        string last_version = "1.0.0.0";
        string start_patch_filename = "test_";
        string format_patch_name = ".patch";
        string patches_url = "http://host/Win/x86/";
        string version_url = "http://host/Win/x86/";
        string[] versions;

        BackgroundWorker worker;
        AutoResetEvent patchDownloadEvent;
        Exception downloadingException;
        private string filename;

         

        public PatcherWindow()
        {
            InitializeComponent();
           
            worker = new BackgroundWorker();
        }

        private void PatcherWindow_Load(object sender, EventArgs e)
        {
            patchDownloadEvent = new AutoResetEvent(false);
            Program.target_dir = Application.StartupPath;
            label1.Text = current_version+"/"+last_version;
            direct_update = Settings.Default.DirectUpdate;
            game_name = Settings.Default.GameName;
            current_version = "1.0.0.0";
            last_version = "1.0.0.0";
            start_patch_filename = Settings.Default.StartPatchFileName;
            format_patch_name = Settings.Default.FormatPatchName;
            patches_url = Settings.Default.PatchesUrl;
            version_url = Settings.Default.VersionUrl;
            text_status("Initialization...");
                worker.WorkerReportsProgress = true;
                worker.WorkerSupportsCancellation = true;
                worker.DoWork += new DoWorkEventHandler(Update);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(UpdateDone);
                worker.RunWorkerAsync();
          
        }
     
       void AfterUpdate()
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(Path.Combine(Application.StartupPath, game_name + ".exe"));
            process.Start();
            this.Close();
        }
   
        string[] transcript_versions(string versions_string)
        {

            string localsave = versions_string;
            string resultsave = "";
            string[] array = new string[50];
            int i = 0;
            while (localsave.IndexOf('&') != -1)
            {
                int index = localsave.IndexOf('&');
                resultsave = localsave.Remove(index, localsave.Length - index);
                array[i] = resultsave;
                localsave = localsave.Remove(0, index + 1);
                i++;
            }
            array[i] = localsave;
            string[] cache_array = new string[i + 1];
            for (int j = 0; j < cache_array.Length; j++)
            {
                cache_array[j] = array[j];

            }
            array = cache_array;
            return array;
        }
        void text_status(string text)
        {
            if (label2.InvokeRequired)
                label2.Invoke(new Action<string>(text_status), text);
            else
                label2.Text = text;
        }
        void progress(int percent)
        {
            if (progressBar1.InvokeRequired)
                progressBar1.Invoke(new Action<int>(progress), percent);
            else
            {
                progressBar1.Value = percent;
                Windows7Taskbar.SetProgressValue(this.Handle, (ulong)percent, (ulong)progressBar1.Maximum);
            }
        }
        void UpdateDone(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                AfterUpdate();
                text_status("Patch done!");
                progress(100);
                return;
            }

            text_status("Update error");
            progress(0);
        }
        void download_done(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
                downloadingException = e.Error;
            else
                downloadingException = null;

            patchDownloadEvent.Set();
        }
    
        void download_progress(object sender, DownloadProgressChangedEventArgs e)
        {
            text_status(string.Format("Download file {0} - [{1}/{2}]Kb - {3} ",
                e.UserState.ToString(),
                PatcherUpdate.HumanReadableSizeFormat(e.BytesReceived/1024),
                PatcherUpdate.HumanReadableSizeFormat(e.TotalBytesToReceive/1024),
                e.ProgressPercentage.ToString()));

            progress(e.ProgressPercentage);
            
        }
   
        void Update(object sender, DoWorkEventArgs e)
        {
           
            progress(0);
            text_status("Checking for new updates...");
            BackgroundWorker worker = (BackgroundWorker)sender;
            try
            {
                current_version = File.ReadAllText(Path.Combine(Application.StartupPath, "CurrentVersion"));
            }
            catch
            {
                e.Cancel = true;
                MessageBox.Show("There is no information about the current version!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            
            if (worker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            try
            {
                string lst_version = "";
                text_status("Check version...");
               
                    System.Net.WebClient webClientVer = new System.Net.WebClient();
                    lst_version = webClientVer.DownloadString(new Uri(version_url + "version"));
               
                    string[] versions_cache = transcript_versions(lst_version);

                int cur_pos_vers = 0;
                for (int i = 0; i < versions_cache.Length; i++)
                {//read lines

                    if (versions_cache[i] == current_version)
                    {
                        cur_pos_vers = i;
                    }
                    else
                    {
                        if (versions_cache.Length == 1) cur_pos_vers = -1;
                    }

                }


                if (cur_pos_vers != -1 && current_version != "start_version")
                {
                    if (versions_cache.Length != 0)
                        versions = new string[versions_cache.Length - cur_pos_vers - 1];

                    int j = 0;
                    for (int i = cur_pos_vers + 1; i < versions_cache.Length; i++)
                    {
                        versions[j] = versions_cache[i];
                        j++;
                    }
                }
                else
                {

                    versions = versions_cache;
                }

                if (versions.Length != 0) last_version = versions[versions.Length - 1];//get last version
                else last_version = current_version;
                     label1.Text = current_version + "/" + last_version;
                
               
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                WebException webex = (WebException)ex;
                HttpWebResponse response = (HttpWebResponse)webex.Response;
                MessageBox.Show("The server version of the file is missing!" + "\nResponse: " + response.StatusDescription, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (worker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }
            if (current_version == last_version)
            {
                text_status("Update not required");
                label1.Text = current_version + "/" + current_version;
                progress(100);
            }
            else
            {
                if (direct_update)
                {
                    System.Net.WebClient webClient = new System.Net.WebClient();
                    webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(download_progress);
                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(download_done);
                   
                    string temp_patch = Path.GetTempFileName();
                    string patch_filename = start_patch_filename + string.Format("{0}_{1}" + format_patch_name, current_version.Replace('.', '_'), last_version.Replace('.', '_'));
                    string patch_uri = patches_url + patch_filename;
               
                    patchDownloadEvent.Reset();
                    try
                    {
                        
                            webClient.DownloadFileAsync(new Uri(patch_uri), temp_patch, patch_filename);
                    
                    }
                    catch (UriFormatException)
                    {
                        e.Cancel = true;
                        MessageBox.Show("There is no file on the server or the server is unavailable.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    patchDownloadEvent.WaitOne();

                        if (downloadingException != null)
                        {
                            WebException webex = (WebException)downloadingException;
                            HttpWebResponse response = (HttpWebResponse)webex.Response;
                            e.Cancel = true;
                            MessageBox.Show("\nMessage: " + webex.Message + "\nRequest: " + (response != null ? response.ResponseUri.ToString() : "null") + "\nResponse: " + (response != null ? response.StatusDescription : "null"), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    
                   
                    try
                    {
                        FileInfo fi = new FileInfo(temp_patch);
                        if (fi.Length != 0)
                        {
                            PatcherUpdate.StartPatch(temp_patch, new Action<string, int>(delegate(string text, int percent)
                             {
                                 text_status(text);
                                 progress(percent);
                             }));
                        }
                        else
                        {
                            e.Cancel = true;
                            MessageBox.Show("Patch file not downloaded!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format("Patch file is incorrect: {0}\n{1}", ex.Message, ex.StackTrace), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        e.Cancel = true;
                        return;
                    }

                    File.Delete(temp_patch);

                }
                else
                {
                    for (int i = 0; i < versions.Length; i++)
                    {
                     System.Net.WebClient webClient = new System.Net.WebClient();
                     
                           webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(download_progress);
                          webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(download_done);
                        
                           string temp_patch = Path.GetTempFileName();
                        string patch_filename = start_patch_filename + string.Format("{0}_{1}" + format_patch_name, current_version.Replace('.', '_'), versions[i].Replace('.', '_'));
                        string patch_uri = patches_url + patch_filename;
                      patchDownloadEvent.Reset();
                       
                        try
                        {
                        
                                webClient.DownloadFileAsync(new Uri(patch_uri), temp_patch, patch_filename);
                       
                          
                        }
                        catch (UriFormatException)
                        {
                            e.Cancel = true;
                            MessageBox.Show("There is no file on the server or the server is unavailable.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                         
                        }
                            patchDownloadEvent.WaitOne();

                            if (downloadingException != null)
                            {
                                WebException webex = (WebException)downloadingException;
                                HttpWebResponse response = (HttpWebResponse)webex.Response;
                                e.Cancel = true;
                                MessageBox.Show("There is no file on the server or the server is unavailable." + "\nMessage: " + webex.Message + "\nRequest: " + (response != null ? response.ResponseUri.ToString() : "null") + "\nResponse: " + (response != null ? response.StatusDescription : "null"), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                      
                            }
                        
                        try
                        {
                            FileInfo fi = new FileInfo(temp_patch);
                            if (fi.Length != 0)
                            {
                                PatcherUpdate.StartPatch(temp_patch, new Action<string, int>(delegate(string text, int percent)
                                 {
                                     text_status(text);
                                     progress(percent);
                                 }));
                            }
                            else
                            {
                                e.Cancel = true;
                                MessageBox.Show("Patch file not downloaded!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                  
                            }
                        
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format("Patch file is incorrect: {0}\n{1}", ex.Message, ex.StackTrace), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            e.Cancel = true;
                            return;
                        }

                       File.Delete(temp_patch);
                        current_version = versions[i];
                        label1.Text = current_version + "/" + last_version;

                    }
                }
                saveCurrentVersionFromFile(current_version);
            }


        }
     
       
        void saveCurrentVersionFromFile(string versionName)
        {
            if (!File.Exists(Path.Combine(Application.StartupPath, "CurrentVersion")))
                File.Create(Path.Combine(Application.StartupPath, "CurrentVersion"));
            File.WriteAllText(Path.Combine(Application.StartupPath, "CurrentVersion"), versionName);
        }
       
       
       
  


        

       
    
    }
    
    }

