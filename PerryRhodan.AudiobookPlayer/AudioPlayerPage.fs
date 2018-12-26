﻿module AudioPlayerPage

open Fabulous
open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open Common
open Domain
open System
open System.Threading.Tasks
open System.IO
open System.Threading
open System.Threading
open Services


    

    type PlayerState =
        | Stopped        
        | Playing

    type Model = 
      { AudioBook:AudioBook 
        CurrentAudioFile: string option
        CurrentAudioFileIndex: int
        CurrentPosition: TimeSpan option  
        CurrentPositionMs: int option 
        CurrentDuration: TimeSpan option  
        CurrentDurationMs: int option
        CurrentState : PlayerState
        AudioFileList: (string * int) list 
        IsLoading: bool 
        CurrentPlayingStateUpdateTimer:Timer option 
        TrackPositionProcess: float
        ProgressbarValue: float         
        TimeUntilSleeps: TimeSpan option 
        AudioPlayerBusy:bool }

    type Msg = 
        | Play 
        | PlayStarted of Timer
        | Stop
        | PlayStopped
        | NextAudioFile
        | PreviousAudioFile
        | JumpForward
        | JumpBackwards
        | FileListLoaded of (string * int) list
        | UpdatePostion of position:int * duration:int
        | ProgressBarChanged of float
        | SaveCurrentPosition //of AudioBook
        | OpenSleepTimerActionMenu
        | StartSleepTimer of TimeSpan option
        | DecreaseSleepTimer
        
        | ChangeBusyState of bool
        | DoNothing

    type ExternalMsg =
        | GotoMainPage
        | GotoBrowserPage


    let audioPlayer = DependencyService.Get<DependencyServices.IAudioPlayer>()

    let toTimeSpan (ms:int) =
        TimeSpan.FromMilliseconds(ms |> float)

    let fromTimeSpan (ts:TimeSpan) =
        ts.TotalMilliseconds |> int
    
    let fromTimeSpanOpt (ts:TimeSpan option) =
        match ts with
        | None -> 0
        | Some ts -> ts |> fromTimeSpan

    


    let loadFilesCommand model =
        async {
            match model.State.DownloadedFolder with
            | None -> return None
            | Some folder ->
                let! files = 
                    asyncFunc( 
                        fun () ->  Directory.EnumerateFiles(folder, "*.mp3")
                    )
                
                let! res =
                    asyncFunc (fun () ->
                        files 
                        |> Seq.toList 
                        |> List.map (
                            fun i ->
                                use tfile = TagLib.File.Create(i)
                                (i,tfile.Properties.Duration |> fromTimeSpan)
                        )
                    )
                return Some (FileListLoaded (res |> List.sortBy (fun (f,_) -> f)))
        } |> Cmd.ofAsyncMsgOption


    let playAudio model =
        (fun (dispatch:Dispatch<Msg>) -> 
            async {
                //let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
                if model.AudioFileList.Length = 0 then
                    return DoNothing
                else
                    let (file,_) = model.AudioFileList.[model.CurrentAudioFileIndex]
                    let currentPosition =model.CurrentPosition |> fromTimeSpanOpt

                    audioPlayer.OnCompletion <- Some (fun ()-> dispatch NextAudioFile)
                    audioPlayer.OnInfo <- Some (fun (p,d) -> 
                        dispatch (UpdatePostion (p,d))
                        let tsPos =  (p |> toTimeSpan)
                        if tsPos.Seconds % 5 = 0 then
                            dispatch (SaveCurrentPosition)
                        )

                    do! audioPlayer.PlayFile file currentPosition

                    let timer = 
                        new Timer(
                            fun _ -> audioPlayer.GetInfo() |> Async.Start
                            ,null,0,1000)

                    
                    return (PlayStarted timer)
            }
        ) |> Cmd.ofAsyncWithInternalDispatch

    let stopAudio model =
        //let audioPlayer = DependencyService.Get<Services.IAudioPlayer>()
        audioPlayer.Stop () |> ignore
        audioPlayer.OnCompletion <- None
        match model.CurrentPlayingStateUpdateTimer with
        | None -> None
        | Some timer ->
            timer.Dispose()
            audioPlayer.LastPositionBeforeStop

    let setAudioPositionRelative value model =
        match model.CurrentPosition with
        | None -> ()            
        |  Some pos ->
            let msPos = pos |> fromTimeSpan
            let newPos = if msPos + value < 0 then 0 else msPos + value
            audioPlayer.GotToPosition (newPos) |> ignore
 
 
    let setAudioPositionAbsolute value =
        audioPlayer.GotToPosition (value) |> ignore

    
    let saveCurrentPosition model =
        async {
            do! Async.Sleep 5000

            match model.CurrentPosition, model.CurrentAudioFile with
            | Some cp, Some file ->
                let newPos = { Position = cp; Filename = file } 
                let newState = { model.AudioBook.State with CurrentPosition = Some newPos }
                let newAudioBook = { model.AudioBook with State = newState }

                let! res =  newAudioBook |> FileAccess.updateAudioBookInStateFile
                match res with
                | Error e ->
                    do! Common.Helpers.displayAlert("Error save Position",e,"Ok")
                    return (Some Stop)
                | Ok _ ->
                    if (model.CurrentState = Stopped) then
                        return None
                    else
                        //return Some (SaveCurrentPosition newAudioBook)
                        return None
            | _,_ ->
                //return Some (SaveCurrentPosition model.AudioBook)
                return None
        } |> Cmd.ofAsyncMsgOption
    
    
    let sleepTimerUpdateCmd model =
        async {
            match model.TimeUntilSleeps with
            | None -> 
                return None
            | Some t ->
                do! Async.Sleep 1000
                return Some DecreaseSleepTimer
        } |> Cmd.ofAsyncMsgOption


    let unsetBusyCmd = Cmd.ofMsg (ChangeBusyState false)


    let setBusyCmd = Cmd.ofMsg (ChangeBusyState true)


    let initModel audioBook = 
        { AudioBook = audioBook; 
          CurrentAudioFile = None
          CurrentAudioFileIndex = 0
          CurrentPosition= None    
          CurrentPositionMs = None
          CurrentDuration= None    
          CurrentDurationMs = None
          CurrentState = Stopped
          AudioFileList = []
          IsLoading=false
          CurrentPlayingStateUpdateTimer=None
          TrackPositionProcess=0.0
          ProgressbarValue = 0.0 
          TimeUntilSleeps = None
          AudioPlayerBusy = false }

    let init audioBook = 
        let model = audioBook |> initModel

        model, Cmd.batch [(audioBook |> loadFilesCommand); setBusyCmd]


    let rec update msg model =
        match msg with
        | Play -> 
            model |> onPlayMsg
        | PlayStarted timer -> 
            model |> onPlayStartedMsg timer          
        | Stop ->
            model |> onStopMsg
        | NextAudioFile -> 
            model |> onNextAudioFileMsg
        | PreviousAudioFile -> 
            model |> onPreviousAudioFileMsg
        | JumpForward -> 
            model |> onJumpForwardMsg            
        | JumpBackwards -> 
            model |> onJumpBackwardsMsg
        | FileListLoaded fileList -> 
            model |> onFileListLoadedMsg fileList
        | UpdatePostion (position, duration) -> 
            model |> onUpdatePositionMsg (position, duration)
        | ProgressBarChanged e -> 
            model |> onProgressBarChangedMsg e
        | SaveCurrentPosition  ->
            model |> onSaveCurrentPosition     
        | OpenSleepTimerActionMenu ->
            model |> onOpenSleepTimerActionMenu        
        | StartSleepTimer sleepTime ->
            model |> onStartSleepTimer sleepTime            
        | DecreaseSleepTimer ->
            model |> onUpdateSleepTimerMsg 
            
        | ChangeBusyState state -> 
            model |> onChangeBusyState state
        | PlayStopped | DoNothing -> 
            model, Cmd.none, None

    
    and onOpenSleepTimerActionMenu model =
        
        let openSleepTimerActionMenu () =            
            async {
                let buttons = [|
                    
                    yield ("off",   (fun () -> StartSleepTimer None)())    
                    yield ("30 sek",(fun () -> StartSleepTimer (Some (TimeSpan.FromSeconds(30.0) ))) ())
                    yield ("5 min", (fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(5.0) ))) ())
                    yield ("15 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(15.0) ))) ())
                    yield ("30 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(30.0) ))) ())
                    yield ("45 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(45.0) ))) ())
                    yield ("60 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(60.0) ))) ())
                    yield ("75 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(75.0) ))) ())
                    yield ("90 min",(fun () -> StartSleepTimer (Some (TimeSpan.FromMinutes(90.0) ))) ())
                        
                |]
                return! Helpers.displayActionSheet (Some "Select Sleep Time ...") (Some "Cancel") buttons
            } |> Cmd.ofAsyncMsgOption

        model, (openSleepTimerActionMenu ()), None
    
    
    and onPlayMsg model = 
        let playAudioCmd = model |> playAudio
        let newModel = {model with CurrentState = Playing}
        newModel, Cmd.batch [ playAudioCmd; model |> saveCurrentPosition ], None


    and onPlayStartedMsg timer model =
        {model with CurrentPlayingStateUpdateTimer = Some timer; AudioPlayerBusy = false }, Cmd.none, None


    and onStopMsg model =
        let lastPosition = 
            model 
            |> stopAudio
            |> Option.map (toTimeSpan)
        let newModel = { model with CurrentState = Stopped; CurrentPlayingStateUpdateTimer = None; CurrentPosition = lastPosition}
        newModel, newModel |> saveCurrentPosition , None

    
    and onNextAudioFileMsg model =
        let max = model.AudioFileList.Length - 1
        let n = model.CurrentAudioFileIndex + 1
        let newIndex = 
            if n > max then max else n
        let (fn,duration) = model.AudioFileList.[newIndex]
        let currentDuration = duration |> toTimeSpan
        let currentDurationMs = duration

        let newModel = 
            { model with 
                CurrentAudioFileIndex = newIndex
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentPosition = None }
            
        if newModel.CurrentState = Playing then            
            newModel |> stopAudio |> ignore

            // do not play "next" file if on end of the audio book
            if (n>max) then
                newModel, Cmd.none, None
            else
                let newModel = { newModel with AudioPlayerBusy = true }
                let playAudioCmd = newModel |> playAudio
                newModel, playAudioCmd, None
        else
            newModel, Cmd.none, None


    and onPreviousAudioFileMsg model =
        let newIndex =                 
            let n = model.CurrentAudioFileIndex - 1
            if n < 0 then 0 else n
        let (fn,duration) = model.AudioFileList.[newIndex]
        let currentDuration = duration |> toTimeSpan
        let currentDurationMs = duration
        let newModel = 
            { model with 
                CurrentAudioFileIndex = newIndex
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentPosition = None }

        if newModel.CurrentState = Playing then
            let newModel = { newModel with AudioPlayerBusy = true }
            newModel |> stopAudio |> ignore
            let playAudioCmd = newModel |> playAudio
            newModel, playAudioCmd, None
        else
            newModel, Cmd.none, None


    and onJumpForwardMsg model =
        model |> setAudioPositionRelative 30000
        model, Cmd.none, None


    and onJumpBackwardsMsg model =
        model |> setAudioPositionRelative -30000
        model, Cmd.none, None


    and onFileListLoadedMsg fileList model =
        match model.AudioBook.State.CurrentPosition with
        | None ->
            let (fn,duration) = fileList.[0]
            let currentDuration = duration |> toTimeSpan
            let currentDurationMs = duration
            {model with 
                AudioFileList = fileList
                CurrentDuration = Some currentDuration
                CurrentDurationMs = Some currentDurationMs
                CurrentAudioFile = Some fn
                CurrentAudioFileIndex = 0
            }, unsetBusyCmd, None
        | Some cp ->
            let lastListenFile = 
                fileList
                |> List.indexed
                |> List.tryFind (fun (_,(fn,_)) -> fn = cp.Filename)
            match lastListenFile with
            | None -> 
                {model with AudioFileList = fileList}, unsetBusyCmd, None
            | Some (idx, (fn, duration)) ->
                let currentPosition = cp.Position
                let currentPositionMs = cp.Position |> fromTimeSpan
                let currentDuration = duration |> toTimeSpan
                let currentDurationMs = duration
                {model with 
                    AudioFileList = fileList
                    CurrentPosition = Some currentPosition
                    CurrentPositionMs = Some currentPositionMs
                    CurrentDuration = Some currentDuration
                    CurrentDurationMs = Some currentDurationMs
                    CurrentAudioFile = Some fn
                    CurrentAudioFileIndex = idx
                }, unsetBusyCmd, None


    and onUpdatePositionMsg (position, duration) model =
        let trackProcess = 
            if (duration = 0) then 0.0
            else
                (position |> float) / (duration |> float)

        {model with 
            CurrentPosition = Some (position |> toTimeSpan)
            CurrentPositionMs = Some position
            CurrentDuration = Some (duration |> toTimeSpan)
            CurrentDurationMs = Some duration
            TrackPositionProcess = trackProcess
        }, Cmd.none, None


    and onProgressBarChangedMsg e model =
        let min = model.TrackPositionProcess - 0.03
        let max = model.TrackPositionProcess + 0.03
        if (e < min || e > max ) then
            if model.CurrentDurationMs.IsSome then
                let newPos = ((model.CurrentDurationMs.Value |> float) * e) |> int
                setAudioPositionAbsolute  newPos

        {model with ProgressbarValue = e}, Cmd.none, None


    and onUpdateSleepTimerMsg model =
        match model.TimeUntilSleeps with
        | None ->
            model, Cmd.none, None
        | Some t ->
            let sleepTime = t.Subtract(TimeSpan.FromSeconds(1.0))
            if sleepTime <= TimeSpan.Zero then
                {model with TimeUntilSleeps = None},Cmd.ofMsg Stop, None
            else
                let newModel = {model with TimeUntilSleeps = Some sleepTime}
                newModel, newModel |> sleepTimerUpdateCmd, None

    
    and onSaveCurrentPosition model =
        model, model |> saveCurrentPosition, None


    and onStartSleepTimer sleepTime model =
        let newModel = {model with TimeUntilSleeps = sleepTime}
        match model.TimeUntilSleeps with
        | None ->
            
            newModel, newModel |> sleepTimerUpdateCmd, None
        | Some _ ->
            newModel, Cmd.none, None


    and onChangeBusyState state model =
        {model with IsLoading = state}, Cmd.none, None



    let view (model: Model) dispatch =
        View.ContentPage(
          title="Player",useSafeArea=true,
          backgroundColor = Consts.backgroundColor,
          content = 
            View.Grid(padding = 20.0,
                horizontalOptions = LayoutOptions.Fill,
                verticalOptions = LayoutOptions.Fill,                
                rowdefs = [ box "*"; box "*"; box "*"; box "auto" ],
                
                children = [

                    // Todo: Data Stuff
                    let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
                    let currentDuration = (model.CurrentDuration |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")
                    yield View.StackLayout(orientation = StackOrientation.Vertical,
                        verticalOptions=LayoutOptions.Fill,
                        horizontalOptions=LayoutOptions.Center,                        
                        children = [
                            yield (Controls.primaryTextColorLabel 25.0 (model.AudioBook.FullName ))
                            yield (Controls.primaryTextColorLabel 40.0 (sprintf "Track: %i von %i" (model.CurrentAudioFileIndex + 1) model.AudioFileList.Length ))
                            yield (Controls.primaryTextColorLabel 30.0 (sprintf "%s von %s" currentPos currentDuration))
                        ]
                    ).GridRow(0)
                    
                    
                    yield View.Image(
                        source=
                            match model.AudioBook.Picture with
                            | None -> "AudioBookPlaceholder_Dark.png"
                            | Some p -> p
                            ,
                        horizontalOptions=LayoutOptions.Fill,
                        verticalOptions=LayoutOptions.Fill,
                        aspect=Aspect.AspectFit
                        
                        ).GridRow(1)
                    
                    let runIfNotBusy (cmd:(unit->unit)) =
                        if not model.AudioPlayerBusy 
                        then cmd
                        else (fun () -> ())
                        
                           

                    yield View.Grid(
                        coldefs=[box "*";box "*";box "*";box "*";box "*"],
                        rowdefs=[box "*";box "*" ],
                        children=[
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch PreviousAudioFile) |> runIfNotBusy) 30.0 true "\uf048").GridColumn(0).GridRow(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch JumpBackwards) |> runIfNotBusy) 30.0 true "\uf04a").GridColumn(1).GridRow(0)

                            match model.CurrentState with
                            | Stopped ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Play) 60.0 false "\uf144").GridColumn(2).GridRow(0)
                            | Playing ->
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch Stop) 60.0 false "\uf28b").GridColumn(2).GridRow(0)
                                
                            
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch JumpForward) |> runIfNotBusy) 30.0 true "\uf04e").GridColumn(3).GridRow(0)
                            yield (Controls.primaryColorSymbolLabelWithTapCommand ((fun () -> dispatch NextAudioFile) |> runIfNotBusy) 30.0 true "\uf051").GridColumn(4).GridRow(0)
                            
                            yield (View.Slider(
                                    value=model.TrackPositionProcess,
                                    minimumMaximum = (0.0,1.0),
                                    //minimum = 0.0, maximum = 1.0, 
                                    horizontalOptions = LayoutOptions.Fill,
                                    valueChanged= (fun e -> dispatch (ProgressBarChanged e.NewValue))
                                  )).GridColumnSpan(5).GridRow(1)

                        ]).GridRow(2)
                    
                    yield View.StackLayout(orientation=StackOrientation.Horizontal,
                            children=[
                                yield (Controls.primaryColorSymbolLabelWithTapCommand (fun () -> dispatch OpenSleepTimerActionMenu) 45.0 true "\uf017")
                                if model.TimeUntilSleeps.IsSome then
                                    let currentSleepTime = (model.TimeUntilSleeps |> Option.defaultValue TimeSpan.Zero).ToString("mm\:ss")
                                    yield (Controls.primaryTextColorLabel 30.0 (sprintf "%s" currentSleepTime))
                            ]
                        ).GridRow(3)
                    
                    if model.IsLoading then 
                        yield Common.createBusyLayer().GridRowSpan(4)
                ]
            )
          
          )

    let viewSmall openPlayerPageCommand (model: Model) dispatch =
        View.Grid(
            coldefs=[box "*"; box "auto"; box "auto"; box "auto"],
            backgroundColor=Consts.cardColor,
            gestureRecognizers = [
                View.TapGestureRecognizer(command=openPlayerPageCommand)
            ],
            children = [
                let currentPos = (model.CurrentPosition |> Option.defaultValue TimeSpan.Zero).ToString("hh\:mm\:ss")

                yield (Controls.primaryTextColorLabel 12.0 (model.AudioBook.FullName ))
                    .GridColumn(0)
                    .With(horizontalOptions=LayoutOptions.Start, margin=Thickness(5.0,0.0,0.0,0.0))
                    

                yield (Controls.primaryTextColorLabel 12.0 (sprintf "%i/%i" (model.CurrentAudioFileIndex + 1) model.AudioFileList.Length ))
                    .GridColumn(1)
                yield (Controls.primaryTextColorLabel 12.0 (sprintf "%s" currentPos))
                    .GridColumn(2)
                match model.CurrentState with
                | Stopped ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Play) 30.0 false "\uf144")
                        .GridColumn(3)
                        .GridRow(0)
                        .With(margin=Thickness(5.0,3.0,5.0,3.0))
                | Playing ->
                    yield (Controls.primaryColorSymbolLabelWithTapCommandRightAlign (fun () -> dispatch Stop) 30.0 false "\uf28b")
                        .GridColumn(3)
                        .GridRow(0)
                        .With(margin=Thickness(5.0,3.0,5.0,3.0))

            ]
        )
    
