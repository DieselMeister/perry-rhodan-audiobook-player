﻿// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace PerryRhodan.AudiobookPlayer.Android

open System

open Android.App
open Android.Content
open Android.Content.PM
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Media
open Android.OS
open Xamarin.Forms.Platform.Android


type AndroidDownloadFolder() =
    interface Services.IAndroidDownloadFolder with
        member this.GetAndroidDownloadFolder () =
            let path = Android.OS.Environment.GetExternalStoragePublicDirectory (Android.OS.Environment.DirectoryDownloads)
            path.AbsolutePath



type AudioPlayer() =
    
    let mutable lastPositionBeforeStop = None
    
    let mutable onCompletion = None
    
    let mediaPlayer = 
        let m = new MediaPlayer()
        
        m.Completion.Add(
            fun _ -> 
                match onCompletion with
                | None -> ()
                | Some cmd -> cmd()
        )
        
        m

    interface Services.IAudioPlayer with
        
        member this.CurrentPosition 
            with get () = mediaPlayer.CurrentPosition
        
        member this.LastPositionBeforeStop with get () = lastPositionBeforeStop

        member this.OnCompletion 
            with get () = onCompletion
            and set p = onCompletion <- p

        member this.PlayFile file position =
            async {
                mediaPlayer.Reset()
                do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask
                mediaPlayer.Prepare()
                mediaPlayer.SeekTo(position)
                mediaPlayer.Start()
                lastPositionBeforeStop <- None
                return ()
            }

        member this.ContinuePlayFile file position =
            async {
                do! mediaPlayer.SetDataSourceAsync(file) |> Async.AwaitTask
                mediaPlayer.SeekTo(position)
                mediaPlayer.Start()
                lastPositionBeforeStop <- None
                return ()
            }
            
        

        member this.Stop () =
            if (mediaPlayer.IsPlaying) then
                mediaPlayer.Pause()
                lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition
                
            else
                lastPositionBeforeStop <- Some mediaPlayer.CurrentPosition

            mediaPlayer.Stop()
            ()

        member this.GotToPosition ms =
            mediaPlayer.SeekTo(ms)
        
        //[<CLIEvent>]
        //member this.OnGotoPositionComplete = onGotoPositionCompleteEvent.Publish
            



[<Activity (Label = "PerryRhodan.AudiobookPlayer.Android", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = (ConfigChanges.ScreenSize ||| ConfigChanges.Orientation),ScreenOrientation = ScreenOrientation.Portrait)>]
type MainActivity() =
    inherit FormsAppCompatActivity()
    override this.OnCreate (bundle: Bundle) =
        FormsAppCompatActivity.TabLayoutResource <- Resources.Layout.Tabbar
        FormsAppCompatActivity.ToolbarResource <- Resources.Layout.Toolbar
        base.OnCreate (bundle)

        Xamarin.Essentials.Platform.Init(this, bundle)

        Xamarin.Forms.Forms.Init (this, bundle)
        Xamarin.Forms.DependencyService.Register<AndroidDownloadFolder>()
        Xamarin.Forms.DependencyService.Register<AudioPlayer>()
        
        Plugin.CurrentActivity.CrossCurrentActivity.Current.Init(this, bundle);

        let appcore  = new PerryRhodan.AudiobookPlayer.App()
        this.LoadApplication (appcore)
    
    override this.OnRequestPermissionsResult(requestCode: int, permissions: string[], [<GeneratedEnum>] grantResults: Android.Content.PM.Permission[]) =
        Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults)
        Plugin.Permissions.PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults)

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults)



