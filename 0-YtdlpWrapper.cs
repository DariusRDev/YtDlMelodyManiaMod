using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniInject;
using UnityEngine;

public class YtdlpWrapper
{
    [Inject]
    private UiManager uiManager;
    public delegate void ProgressChangedHandler(int progressPercent);
    public event ProgressChangedHandler ProgressChanged;

    public async Task<string> DownloadVideoAsync(ModObjectContext modContext, string videoUrl, string downloadsFolder, string songFolder, CancellationToken cancellationToken)
    {
        string newVideoFileName = "";

        var ytdlpExe = $"{modContext.ModFolder}\\yt-dlp.exe";
        if (System.IO.File.Exists(downloadsFolder))
        {
            System.IO.Directory.Delete(downloadsFolder, true);

        }

        System.IO.Directory.CreateDirectory(downloadsFolder);
        // copy to output folder
        if (System.IO.File.Exists(ytdlpExe))
        {
            System.IO.File.Copy($"{ytdlpExe}", $"{downloadsFolder}\\yt-dlp.exe", true);
        }

        var tcs = new TaskCompletionSource<object>();

        // where its run
        string fFmpegPath = ".\\Melody Mania_Data\\StreamingAssets\\ffmpeg\\ffmpeg.exe";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = $"{downloadsFolder}\\yt-dlp.exe",
            Arguments = $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" --merge-output-format mp4 --ffmpeg-location \"{fFmpegPath}\"  -P \"{downloadsFolder}\" -P \"temp:tmp\" {videoUrl} --output %(title)s.%(ext)s ",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,

        };
        UnityEngine.Debug.Log($"yt-dlp.exe {startInfo.Arguments}");
        using (Process process = new Process
        {
            StartInfo = startInfo,

        })
        {
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.SetResult(null);
            process.ErrorDataReceived += Process_ErrorDataReceived;
            process.OutputDataReceived += Process_OutputDataReceived;
            cancellationToken.Register(() => process.Kill());
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            await tcs.Task;
        }

        // copy output file to song folder
        string[] files = System.IO.Directory.GetFiles(downloadsFolder);
        // but only the video
        try
        {
            foreach (string file in files)
            {
                if (file.EndsWith(".mp4"))
                {
                    newVideoFileName = $"{System.IO.Path.GetFileName(file)}";
                    System.IO.File.Copy(file, $"{songFolder}\\{newVideoFileName}", true);

                }
            }
        }
        catch (Exception e)
        {
            uiManager.CreateErrorInfoDialogControl("Error downloading video", "Check if Video is playing in the background. Can't replace video while playing.", e.ToString());

            UiManager.CreateNotification($"The video is not allowed to run while downloading. Please close the video and try again.");
        }
        // delete the rest
        System.IO.Directory.Delete(downloadsFolder, true);

        return newVideoFileName;
    }

    private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            UnityEngine.Debug.Log("Output: " + e.Data);
            Regex regex = new Regex(@"\[download\]\s+(\d+\.\d+)%");
            Match match = regex.Match(e.Data);
            if (match.Success)
            {

                float progress = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                ProgressChanged?.Invoke((int)progress);

            }
        }
    }

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            UiManager.CreateNotification($"Error: {e.Data}");
            UnityEngine.Debug.Log("Error: " + e.Data);
        }
    }

}
