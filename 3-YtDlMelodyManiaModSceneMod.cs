using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniInject;
using UnityEngine;
using UnityEngine.UIElements;

// Mod interface to do something when a scene is loaded.
// Available scenes are found in the EScene enum.
public class YtDlMelodyManiaModSceneMod : IContextMenuMod
{
    // Get common objects from the app environment via Inject attribute.
    [Inject]
    private UIDocument uiDocument;

    [Inject]
    private SceneNavigator sceneNavigator;

    // Mod settings implement IAutoBoundMod, which makes an instance available via Inject attribute
    [Inject]
    private YtDlMelodyManiaModModSettings modSettings;
    [Inject]
    private JobManager jobManager;

    [Inject]
    private ModObjectContext modContext;

    [Inject]
    private SongMetaManager songMetaManager;

    [Inject]
    private UiManager uiManager;

    private readonly List<IDisposable> disposables = new List<IDisposable>();


    public void FillContextMenu(ContextMenuPopupControl contextMenu)
    {
        if (contextMenu.Context.GetType() == typeof(SongSelectEntryControl))
        {
            SongSelectEntryControl songSelectEntryControl = (SongSelectEntryControl)contextMenu.Context;

            if (songSelectEntryControl.SongSelectEntry is SongSelectSongEntry songSelectSongEntry)
            {
                if (songSelectSongEntry.SongMeta.Website.IsNullOrEmpty())
                {
                    return;
                }
                string youtubeDLPath = $"{modContext.ModFolder}\\yt-dlp.exe";


                contextMenu.AddButton("YT-Download", "download", async () =>
                {
                    if (File.Exists(youtubeDLPath))
                    {
                        await DownloadVideo(songSelectSongEntry.SongMeta);
                    }
                    else
                    {
                        MessageDialogControl messageDialogControl = uiManager.CreateDialogControl("'yt-dlp.exe' not found");
                        VisualElement content = new VisualElement();
                        content.Add(new Label(@"This mod uses the Yt-Dlp Project to downlaod YouTube Videos.
Please download 'yt-dlp.exe' from Github and place it in the mod folder.
https://github.com/yt-dlp/yt-dlp/releases"));
                        Button button = new Button(() => Application.OpenURL("https://github.com/yt-dlp/yt-dlp/releases"));
                        button.text = "Open Github (yt-dlp.exe)";
                        content.Add(button);
                        button.AddToClassList("mb-2");
                        button.AddToClassList("mt-2");
                        Button openModFolder = new Button(() => Application.OpenURL(modContext.ModFolder));
                        openModFolder.text = "Open Mod Folder";
                        content.Add(openModFolder);
                        messageDialogControl.AddVisualElement(content);
                    }
                });



            }
        }
    }




    public async Task DownloadVideo(SongMeta songMeta)
    {
        UnityEngine.Debug.Log("DownloadVideo");
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        Job job = new Job("Downloading Youtube Video for " + songMeta.Title);
        job.OnCancel = () => cancellationTokenSource.Cancel();
        job.EstimatedCurrentProgressInPercent = 0;
        job.SetStatus(EJobStatus.Running);
        jobManager.AddJob(job);
        try
        {
            YtdlpWrapper ytdlpWrapper = new YtdlpWrapper();
            ytdlpWrapper.ProgressChanged += (progressPercent) =>
            {
                job.EstimatedCurrentProgressInPercent = progressPercent;
            };

            // set the path of yt-dlp and FFmpeg if they're not in PATH or current directory
            string youtubeDLPath = $"{modContext.ModFolder}\\yt-dlp.exe";
            string fFmpegPath = $"{modContext.ModFolder}\\ffmpeg.exe";
            // optional: set a different download folder
            // download a video
            string downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads\\MelodyManiaYtdlTemp";
            string newVideoFileName = await ytdlpWrapper.DownloadVideoAsync(modContext, songMeta.Website, downloadsFolder, songMeta.FileInfo.DirectoryName, cancellationTokenSource.Token);
            // the path of the downloaded file
            Debug.Log("DOWNLOADADDEED");


            songMeta.Video = newVideoFileName;
            songMetaManager.SaveSong(songMeta, false);
            songMeta.Audio = newVideoFileName;

            // Update the job status
            job.SetStatus(EJobStatus.Finished);
            job.SetResult(EJobResult.Ok);
        }
        catch (System.Exception e)
        {
            job.SetResult(EJobResult.Error);
            job.SetStatus(EJobStatus.Finished);
            UnityEngine.Debug.Log(e);
            UnityEngine.Debug.LogException(e);
        }
    }
}



