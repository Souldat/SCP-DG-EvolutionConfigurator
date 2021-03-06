﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.Collections.Specialized;
using Renci.SshNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;


namespace EvolutionConfigurator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    ///     

    public partial class MainWindow : Window
    {
        NameValueCollection appSettings;
        string repoDir = Directory.GetCurrentDirectory() + "\\Repos";

        Dictionary<string, string> dictPi = new Dictionary<string, string>();
        string deviceIP;

        public MainWindow()
        {
            InitializeComponent();

            dictPi = FindNetworkedPi();

            foreach(string str in dictPi.Values)
            {
                Devices_combobox.Items.Add(str);
            }

            //Get app settings
            ConfigurationManager.RefreshSection("appSettings");
            appSettings = ConfigurationManager.AppSettings;
           
            //Populate product combobox
            Product_combobox.ItemsSource = appSettings;

            DirectoryInfo dir = new DirectoryInfo(repoDir);            

            //Create any repo directories if they don't already exist (this will gracefully fail if the repo already exists locally)
            for (int i=0;i<appSettings.Count; i++)
            {
                //RunGitCommand("clone", appSettings[i], repoDir);

                //Only clone if we don't already have the repo (takes time to run the git command and come back)
                if (!Directory.Exists(repoDir + "\\" + appSettings.GetKey(i)))
                {
                    //Directory.CreateDirectory(repoDir + "\\" + appSettings.GetKey(i));
                    RunGitCommand("clone", appSettings[i], repoDir);
                }
            }

            //Get directory listing after copying repos
            DirectoryInfo[] dirs = dir.GetDirectories();

            //Pull any updates from each repo
            foreach (DirectoryInfo diri in dirs)
            {
                RunGitCommand("pull", "--all", diri.FullName);
            }

            //Pull any updated tags from each repo and clean up stale tags
            foreach (DirectoryInfo diri in dirs)
            {               
                RunGitCommand("fetch", "--prune --prune-tags", diri.FullName);
            }            

            Product_combobox.SelectedIndex = 0;    
        }
         
        string GetRepos(string path)
        {
            string[] dir = Directory.GetDirectories(path);

            return dir[0];
        }

        public void PopulateSoftwareVersion()
        {      
            var tags = RunGitCommand("tag", "", GetRepos(Directory.GetCurrentDirectory() + "\\Repos\\" + Product_combobox.SelectedItem.ToString())); // get all tags            
            byte[] byteArray = Encoding.ASCII.GetBytes(tags);
            MemoryStream stream = new MemoryStream(byteArray);

            StreamReader objstream = new StreamReader(stream);

            string[] Parts = null;

            while (objstream.Peek() >= 0)
            {
                Parts = objstream.ReadLine().Split(new char[] { ',' });
            }

            //Drop last index and reverse order 
            //From the array creation above, the last new line gets a "," and so its split leaving
            //An empty value at the last index
            for (int i = (Parts.Length - 2); i >= 0; i--)
            {
                Tags_combobox.Items.Add(Parts[i]);
            }
        }
               
        public static void DeleteReadOnlyDirectory(string directory)
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                DeleteReadOnlyDirectory(subdirectory);
            }
            foreach (var fileName in Directory.EnumerateFiles(directory))
            {
                var fileInfo = new FileInfo(fileName);
                fileInfo.Attributes = FileAttributes.Normal;
                fileInfo.Delete();
            }
            Directory.Delete(directory);
        }
        
        public void DeleteFolderContents(DirectoryInfo dir)
        {
            foreach (FileInfo file in dir.GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo direc in dir.GetDirectories())
            {
                //If the folder (.git) is read only catch the error and remove read only flags then delete
                try
                {
                    dir.Delete(true);
                }
                catch (Exception)
                {
                    DeleteReadOnlyDirectory(direc.FullName);
                }
            }
        }
        
        public Dictionary<string, string> FindNetworkedPi()
        {
            object command = "arp -a | findstr b8-27-eb";
            // create the ProcessStartInfo using "cmd" as the program to be run,
            // and "/c " as the parameters.
            // Incidentally, /c tells cmd that we want it to execute the command that follows,
            // and then exit.
            System.Diagnostics.ProcessStartInfo procStartInfo =
                new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

            // The following commands are needed to redirect the standard output.
            // This means that it will be redirected to the Process.StandardOutput StreamReader.
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            // Do not create the black window.
            procStartInfo.CreateNoWindow = true;
            // Now we create a process, assign its ProcessStartInfo and start it
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            // Get the output into a string
            string result = proc.StandardOutput.ReadToEnd();
            // Display the command output.

            //Clean up results
            string[] array = result.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);            

            //KeyValuePair<string, string> kvp = new KeyValuePair<string, string>();

            Dictionary<string, string> dict = new Dictionary<string, string>();            
                
            foreach(string strs in array)//Array of strings
            {
                string key;
                string value;

                //wrap your brain around this one! ~_^
                key = strs.Remove(0, 2);
                value = key.Remove(0, 22);
                value = value.Remove(17);                
                key = key.Remove(13); 
                
                dict.Add(key, value);                
            }

            return dict;
        }            
        
        public string RunGitCommand(string command, string args, string workingDirectory)
        {
            //workingDirectory = repoDir;

            string git = "git";
            var results = "";
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = git,
                    Arguments = $"{command} {args}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory,
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                results += $"{proc.StandardOutput.ReadLine()},";
            }
            proc.WaitForExit();
            return results;
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            string selectedRepositoryName = Product_combobox.SelectedItem.ToString();

            //Clone specified tag version to the temp folder in preperation for copying to the Evo unit
            RunGitCommand("clone", "--branch " + Tags_combobox.SelectedItem + " " + appSettings.Get(selectedRepositoryName), Directory.GetCurrentDirectory() + "\\Temp"); // get all tags

     
            //Get selected IP from drop down menu
            for (int i = 0; i < dictPi.Count; i++)
            {
                if (dictPi.Values.ElementAt(i) == Devices_combobox.SelectedItem.ToString())
                {
                    deviceIP = dictPi.Keys.ElementAt(i).ToString();
                }
            }           

            //Setup sftp connection outside of recursive copy be sure to close this connection after remote copy is finished
            SftpClient sftpClient = new SftpClient(deviceIP, "pi", "1X393c4db2");
            sftpClient.Connect();

            //Check if folder already exists on linux system. Would be nice to report the version number found in the future.
            if(sftpClient.Exists("/home/pi/PFC/" + selectedRepositoryName))
            {
                var msgBoxresult = MessageBox.Show("Target system already contains the selected software.\n\nWould you like to overwrite?", "Repository Already Present", MessageBoxButton.YesNo, MessageBoxImage.Warning);
               
                if (msgBoxresult == MessageBoxResult.Yes)
                {
                    LinuxRemoteCopy(sftpClient, deviceIP, "pi", "1X393c4db2", Directory.GetCurrentDirectory() + "\\Temp\\" + selectedRepositoryName, "/home/pi/PFC/" + selectedRepositoryName, true, true);
                }                 
            }
            else
            {
                //Folder doesn't exist just copy
                LinuxRemoteCopy(sftpClient, deviceIP, "pi", "1X393c4db2", Directory.GetCurrentDirectory() + "\\Temp\\" + selectedRepositoryName, "/home/pi/PFC/" + selectedRepositoryName,  true, false);
            }

            //Disconnect ftp client
            sftpClient.Disconnect();

            //Delete Temp Folder Contents            
            DeleteFolderContents(new DirectoryInfo(Directory.GetCurrentDirectory() + "\\Temp"));
        }

        /*
         * A little redundant that this function requires a deviceip and user and password when you're handing it an already connected sftpclient
         * but for the sake of making this a little more reusable I've made it this way. This is due to the requirement of using an ssh client
         * to remove destination folders if required. Solution is to make this ssh directory removal a recursive sftpClient function.
         * TODO: Write recursive delete for SftpClient. 
         */
        /// <summary>
        /// <b>Expects SftpClient object to already be in a connected state.</b><br/>
        /// Recursively copies all files and folders under the base directory given including creating the base directory itself at the target location.<br/>
        /// Target location string is required to be in unix format. eg. /home/pi/PFC/scp-dg-doorpanel
        /// </summary>
        /// <param name="sftpClient">SftpClient object is required to be connected.</param>
        /// <param name="sourceDirName">Path to directory of source files. This directory itself is copied and all contents.</param>
        /// <param name="destDirName">Path to folder you wish your selected source folder to be copied to. (unix file path format required. eg. /home/pi/cooldevstuff)</param>
        /// <param name="copySubDirs">Set to true for a recursive file copy. Otherwise only contents of root folder are copied.</param>
        /// <param name="purgeDestination">Set this to true if you wish for any files / folders at the destination to be removed replaced.</param>
        void LinuxRemoteCopy(SftpClient sftpClient, string deviceIP, string userName, string password, string sourceDirName, string destDirName, bool copySubDirs, bool purgeDestination)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            DirectoryInfo[] dirs = dir.GetDirectories();

            if (purgeDestination)
            {
                /*        
                * An ugly hack to forceably delete the directory. SftpClient does not natively support a recursive delete
                * function so rather than writing up a whole recursive delete function myself, just connect with an ssh client and
                * remove the directory with regular shell commands. ¯\_(ツ)_/¯
                */
                SshClient ssh = new SshClient(deviceIP, userName, password);
                ssh.Connect();
                ssh.RunCommand("sudo rm -rf " + destDirName);
                ssh.Disconnect();
            }        

            sftpClient.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = System.IO.Path.Combine(destDirName, file.Name);
                temppath = temppath.Replace("\\", "/");
                //file.CopyTo(temppath, false);

                using (var fileStream = File.OpenRead(file.FullName))
                {
                    sftpClient.UploadFile(fileStream, temppath, true);
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = System.IO.Path.Combine(destDirName, subdir.Name);
                    temppath = temppath.Replace("\\", "/");                             //Don't delete stuff on recursive calls, only delete root folder on initial call
                    LinuxRemoteCopy(sftpClient,deviceIP, userName, password, subdir.FullName, temppath, copySubDirs, false);
                }
            }
        }
        
        private void Tags_cmbobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Product_combobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Tags_combobox.Items.Clear();
            PopulateSoftwareVersion();
        }
    }
}
