﻿module AudioPlayerState

    open Domain
    open FSharp.Control

    let jumpDistance = 30000

    type AudioPlayerServiceState =
        | Stopped        
        | Started

    type AudioPlayerState =
        | Playing
        | Stopped


    type Mp3FileList = (string * int) list
        
    type AudioPlayerInfo =
        { Filename: string
          Position: int
          Duration: int
          CurrentTrackNumber: int
          State: AudioPlayerState 
          AudioBook: AudioBook
          Mp3FileList: Mp3FileList
          PlaybackDelayed: bool 
          ResumeOnAudioFocus: bool 
          ServiceState: AudioPlayerServiceState }
        
        static member Empty =
            { Filename = ""
              Position = 0
              Duration = 0
              CurrentTrackNumber = 0
              State = Stopped 
              AudioBook = AudioBook.Empty
              Mp3FileList = [] 
              PlaybackDelayed = false
              ResumeOnAudioFocus = false 
              ServiceState = AudioPlayerServiceState.Stopped }


    type IAudioPlayer = 

        abstract member RunService: AudioBook -> Mp3FileList -> unit
        abstract member StopService: unit -> unit

        abstract member StartAudio: string->int -> unit
        abstract member StopAudio: unit -> unit
        abstract member TogglePlayPause: unit -> unit
        abstract member MoveForward: unit -> unit
        abstract member MoveBackward: unit -> unit
        abstract member GotToPosition: int -> unit
        abstract member JumpForward: unit -> unit
        abstract member JumpBackward: unit -> unit

        abstract member GetCurrentState: unit -> Async<AudioPlayerInfo option>

        //abstract member GetCurrentState: unit -> Async<AudioPlayerInfo option>
    


    
    type AudioPlayerCommand =
        | StartAudioService of AudioBook * Mp3FileList
        | StopAudioService
        | StartAudioPlayer
        | StartAudioPlayerExtern of filename:string * position: int
        | StopAudioPlayer of resumeOnAudioFocus:bool
        | TogglePlayPause
        | MoveToNextTrack
        | MoveToPreviousTrack
        | JumpForward 
        | JumpBackwards 
        | SetPosition of pos:int
        | UpdatePositionExternal of pos:int
        | SetCurrentAudioServiceStateToStarted


        | GetCurrentState of AsyncReplyChannel<AudioPlayerInfo>



    type IAudioServiceImplementation =
        abstract member StartAudioService: AudioPlayerInfo -> AudioPlayerInfo
        abstract member StopAudioService: AudioPlayerInfo -> AudioPlayerInfo
        abstract member StartAudioPlayer: AudioPlayerInfo -> Async<AudioPlayerInfo>
        abstract member StopAudioPlayer: AudioPlayerInfo -> AudioPlayerInfo
        abstract member MoveToNextTrack: AudioPlayerInfo -> Async<AudioPlayerInfo>
        abstract member MoveToPreviousTrack: AudioPlayerInfo -> Async<AudioPlayerInfo>
        abstract member SetPosition: AudioPlayerInfo -> AudioPlayerInfo
        abstract member OnUpdatePositionNumber: AudioPlayerInfo -> AudioPlayerInfo
        abstract member StateMailbox:MailboxProcessor<AudioPlayerCommand> with get


    

    type AudioPlayerEvents =
        | AudioServiceStarted of state:AudioPlayerInfo
        | AudioServiceStopped of state:AudioPlayerInfo
        | AudioPlayerStarted of state:AudioPlayerInfo       
        | AudioPlayerStopped of state:AudioPlayerInfo
        | MovedToNextTrack of state:AudioPlayerInfo
        | MovedToPreviousTrack of state:AudioPlayerInfo
        | PositionSet of state:AudioPlayerInfo



    module Helpers =
        
        
        let getIndexForFile (currentMp3ListWithDuration:Mp3FileList) file =
            currentMp3ListWithDuration |> List.findIndex (fun (name,_) -> name = file)


        let getFileFromIndex (currentMp3ListWithDuration:Mp3FileList) idx =
            let idx =
                if idx < 0 then 0
                elif idx > (currentMp3ListWithDuration.Length - 1) then (currentMp3ListWithDuration.Length - 1)
                else idx

            currentMp3ListWithDuration.[idx]


        let storeCurrentAudiobookState info =
            let abPos = { Filename = info.Filename; Position = info.Position |> Common.TimeSpanHelpers.toTimeSpan }
            let newAb = {info.AudioBook with State = {info.AudioBook.State with CurrentPosition = Some abPos; LastTimeListend = Some System.DateTime.UtcNow } }
            let res = (Services.FileAccess.updateAudioBookInStateFile newAb) |> Async.RunSynchronously
            match res with
            | Error e ->
                Microsoft.AppCenter.Crashes.Crashes.TrackError(exn("narf pos nicht gespeichert! Msg:" + e))
            | Ok () ->
                ()


    module InformationDispatcher =


        type InfoDispatcherMsg =
            | AddListener of (string * (AudioPlayerInfo -> Async<unit>))
            | RemoveListener of string
            | Dispatch of AudioPlayerInfo


        let audioPlayerStateInformationDispatcher =
            MailboxProcessor<InfoDispatcherMsg>.Start(
                fun inbox ->
                    let rec loop state =
                        async {
                            let! msg = inbox.Receive()

                            let newState =
                                match msg with
                                | AddListener (key,handler) ->
                                    if not (state |> List.exists (fun (k,_) -> k = key)) then
                                        state @ [(key,handler)]
                                    else
                                        state
                                | RemoveListener key ->
                                    state |> List.filter (fun (k,_) -> k <> key)
                                | Dispatch info ->
                                    let aseq =
                                        asyncSeq {
                                            for (_,handler) in state do
                                                do! handler(info)
                                        }
                                        |> AsyncSeq.toList
                                    state
                                    
                                             
                            do! loop newState
                        }

                    loop []
            )

    

    let audioPlayerStateMailbox         
        (audioService:IAudioServiceImplementation)
        (informationDispatcher: MailboxProcessor<InformationDispatcher.InfoDispatcherMsg>) =        
        MailboxProcessor<AudioPlayerCommand>.Start(
            fun inbox ->
                
                let rec loop state =
                    async {
                        try
                            let! command = inbox.Receive()

                            match state.ServiceState with
                            | AudioPlayerServiceState.Stopped ->
                                match command with
                                | StartAudioService (ab,mp3List) ->
                                    let newState = 
                                        { state with
                                            AudioBook = ab
                                            Mp3FileList = mp3List                                             
                                        }
                                        |> audioService.StartAudioService                                    
                                    let newState = {newState with ServiceState = Started }  
                                    informationDispatcher.Post(InformationDispatcher.InfoDispatcherMsg.Dispatch newState)
                                    return! (loop newState)

                                | GetCurrentState reply ->
                                    reply.Reply(state)
                                    return! (loop state)

                                | _ -> 
                                    // if audioservice stopped ignore commands
                                    return! (loop state)


                            | Started ->                                
                                let! (newState) = processCommandsWhenStated command state
                                informationDispatcher.Post(InformationDispatcher.InfoDispatcherMsg.Dispatch newState)
                                return! loop newState
                                

                        with
                        | _ as ex ->
                            Microsoft.AppCenter.Crashes.Crashes.TrackError(ex)
                            return! loop state
                    }


                and processCommandsWhenStated command state =
                    async {
                        match command with
                        | StartAudioService (ab,mp3List) ->
                            let newState = 
                                state |> onStartService ab mp3List
                            return (newState)
                        | StopAudioService ->
                            let newState = 
                                state |> onStopService                             
                            return (newState)
                        | StartAudioPlayer ->                                    
                            let! newState = 
                                state |> onStartPlayer state.Filename state.Position                            
                            return newState
                        | StartAudioPlayerExtern (filename, pos) ->                                    
                            let! newState = state |> onStartPlayer filename pos
                            return newState
                        | StopAudioPlayer resumeOnAudioFocus ->
                            let newState = state |> onStopPlayer resumeOnAudioFocus
                            return newState
                        | TogglePlayPause ->
                            match state.State with
                            | Playing ->
                                let newState = state |> onStopPlayer false
                                return newState
                            | Stopped ->
                                let! newState = state |> onStartPlayer state.Filename state.Position
                                return (newState)                                  
                        | MoveToNextTrack ->
                            let! newState = state |> onMoveNextTrack 0
                            return (newState)
                        | MoveToPreviousTrack ->
                            let! newState =
                                if state.Position > 2000 then
                                    state |> onSetPosition 0
                                else
                                    state |> onMovePreviousTrack 0
                            return (newState)
                        | JumpForward ->
                            let! newState = state |> onJumpForward
                            return (newState)
                        | JumpBackwards ->
                            let! newState = state |> onJumpBackward
                            return (newState)
                        | SetPosition pos ->
                            let! newState = state |> onSetPosition pos
                            return (newState)
                        | UpdatePositionExternal pos ->
                            let newState = state |> onUpdatePositionExternal pos
                            return (newState)
                        | SetCurrentAudioServiceStateToStarted ->
                            let newState = {state with ServiceState = Started }
                            return (newState)

                        | GetCurrentState reply ->
                            reply.Reply(state)
                            return (state)
                    }


                and onUpdatePositionExternal pos state =
                    let newState = {state with Position = pos }
                    let secs = (pos |> Common.TimeSpanHelpers.toTimeSpan).Seconds
                    if secs = 0 || secs = 5 then
                        Helpers.storeCurrentAudiobookState newState
                    audioService.OnUpdatePositionNumber newState    
                    


                and onStartService ab mp3List state =
                    { state with
                        AudioBook = ab
                        Mp3FileList = mp3List                            
                    } |> audioService.StartAudioService
                    
                
                and onStopService state =
                    let newState =
                        state |> audioService.StopAudioService
                    Helpers.storeCurrentAudiobookState newState
                    {newState with ServiceState = AudioPlayerServiceState.Stopped }



                and onStopPlayer resumeOnAudioFocus state =
                        let newState = 
                            { state with
                                ResumeOnAudioFocus = resumeOnAudioFocus
                                PlaybackDelayed = false }
                            |> audioService.StopAudioPlayer

                        { newState with State = Stopped }


                and onStartPlayer filename pos state =
                    async {
                        let index = filename |> Helpers.getIndexForFile state.Mp3FileList
                        let (_,duration) = index |> Helpers.getFileFromIndex state.Mp3FileList
                        let newState =
                            { state with
                                Filename = filename
                                Position = pos 
                                ResumeOnAudioFocus = true 
                                CurrentTrackNumber = index + 1
                                Duration = duration
                                PlaybackDelayed = false }
                        let! newState = audioService.StartAudioPlayer newState
                        Helpers.storeCurrentAudiobookState newState
                        return { newState with State=Playing }
                    }
                    


                and onMoveNextTrack pos state =
                    async {
                        let index =
                            state.Filename
                            |> Helpers.getIndexForFile state.Mp3FileList

                        let newIndex = index + 1
                        
                        if newIndex > (state.Mp3FileList.Length - 1) then

                            // Let's stop the player
                            let newState = state |> onStopPlayer false

                            let newAb = {newState.AudioBook with State = {newState.AudioBook.State with LastTimeListend = Some System.DateTime.UtcNow; Completed = true } }
                            let newState = {newState with AudioBook = newAb }

                            // ToTo store state on disk ?!
                            return newState
                        else
                            let (newFile,newDuration) = newIndex |> Helpers.getFileFromIndex state.Mp3FileList
                            let newState = {state with Filename = newFile; Duration = newDuration; Position = pos; CurrentTrackNumber = newIndex + 1}
                            let! newState = newState |> audioService.MoveToNextTrack 
                            return newState 
                    }
                    


                and onMovePreviousTrack pos state =
                    async {
                        let index =
                            state.Filename
                            |> Helpers.getIndexForFile state.Mp3FileList

                        let newIndex = 
                            index - 1

                        // check if index okay in get file function
                        let (newFile,newDuration) = newIndex |> Helpers.getFileFromIndex state.Mp3FileList
                        let newState = {state with Filename = newFile; Duration = newDuration; Position = pos; CurrentTrackNumber = newIndex + 1}
                        let! newState = newState |> audioService.MoveToPreviousTrack
                        return newState
                    }
                    


                and onJumpForward state =
                    async {
                        let newPos = state.Position + jumpDistance                            
                        return! state |> onSetPosition newPos
                    }
                    


                and onJumpBackward state =
                    async {
                        let newPos = state.Position - jumpDistance 
                        return! state |> onSetPosition newPos
                    }
                    


                and onSetPosition pos state = 
                    async {
                        let setPosOnCurrentTrack pos (apinfo:AudioPlayerInfo) =
                            async {
                                return {apinfo with Position = pos}
                            }
                            

                        let! newState =
                            // when your new pos is actually on the next track
                            if pos > state.Duration then
                                let diff = pos - state.Duration
                                state |> onMoveNextTrack diff
                            // when you new position is actually on the previous track
                            elif pos < 0 then
                                let (file,durationPrevTrack) = (state.CurrentTrackNumber - 2) |> Helpers.getFileFromIndex state.Mp3FileList
                                // are we already on the first track
                                if file = state.Filename then 
                                    state |> setPosOnCurrentTrack 0
                                else
                                    let posPrevTrack = durationPrevTrack + pos
                                    state |> onMovePreviousTrack posPrevTrack
                            // no edge case                        
                            else
                                state |> setPosOnCurrentTrack pos
                        

                        // try to aggregate and process the jump and setpos messages without calling the 
                        // underlying audio player implementation and only change the state and at the end
                        // run the implemention
                        let possibleOtherSetPosMessageResult =
                            inbox.TryScan( 
                                fun cmd ->                                    
                                    match cmd with
                                    | JumpForward ->
                                        let jumpF = onJumpForward newState
                                        Some (jumpF)
                                    | JumpBackwards ->
                                        let jumpB = onJumpBackward newState
                                        Some jumpB
                                    //| SetPosition pos ->  
                                    //    let setPo = onSetPosition pos newState
                                    //    Some setPo
                                    | _ ->
                                        None
                                , 1000
                            )
                        let! res = possibleOtherSetPosMessageResult
                        match res with
                        | None ->
                            return newState |> audioService.SetPosition
                        | Some newState ->
                            informationDispatcher.Post(InformationDispatcher.InfoDispatcherMsg.Dispatch newState)
                            return newState
                        
                    }
                    
                    

                
                loop AudioPlayerInfo.Empty                   
    )






        

