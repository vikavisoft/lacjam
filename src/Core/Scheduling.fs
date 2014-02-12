﻿namespace Lacjam.Core

module Scheduling =
    open Autofac
    open Lacjam
    open Lacjam.Core
    open Lacjam.Core.Domain
    open Lacjam.Core.Runtime
    open NServiceBus
    open System
    open System.Collections.Concurrent
    open System.Collections.Generic
    open System.IO
    open System.Net
    open System.Linq
    open System.Net.Http
    open System.Runtime.Serialization
    open System.Text.RegularExpressions
    open Quartz
    open Quartz.Spi
    open Quartz.Impl
                                                                 
   
    type IJobScheduler = 
            abstract scheduleBatch<'a when 'a :> IJob> : Lacjam.Core.Batch * TriggerBuilder -> unit
            abstract scheduleBatch<'a when 'a :> IJob> : Lacjam.Core.Batch -> unit
            abstract member processBatch : Batch -> unit
            abstract member Scheduler : IScheduler with get 
            abstract member createTrigger : TriggerBuilder  with get, set
    

    type BatchMessage = ILogWriter * IBus * Jobs.JobMessage * AsyncReplyChannel<Jobs.JobResult>

    type JobSchedulerListener(log:ILogWriter, bus:IBus) =
        interface Quartz.ISchedulerListener with
            override this.JobAdded(jd) = log.Write(Debug("JobAdded: " + jd.JobType.Name.ToString() + " " + jd.Key.Group + " " + jd.Key.Name))
            override this.JobDeleted(jd) = log.Write(Debug("JobDeleted: " + jd.Group + " " + jd.Name))
            override this.JobPaused(jd) = log.Write(Debug("JobPaused: " + jd.Group + " " + jd.Name))
            override this.JobsPaused(jd) = log.Write(Debug("JobsPaused: " + jd))
            override this.JobResumed(jd) = log.Write(Debug("JobResumed: " + jd.Group + " " + jd.Name))
            override this.JobsResumed(jd) = log.Write(Debug("JobsResumed: " + jd))
            override this.JobScheduled(jd) = log.Write(Debug("JobScheduled: " + jd.JobKey.Group + " " + jd.JobKey.Name + " " + jd.Key.Group + " " + jd.Key.Name))
            override this.JobUnscheduled(jd) = log.Write(Debug("JobUnScheduled: " + jd.Group + " " + jd.Name))
            override this.TriggerFinalized(trg) = log.Write(Debug("TriggerFinalized: " + trg.Key.Group + " " + trg.Key.Name))
            override this.TriggerPaused(trg) = log.Write(Debug("TriggerPaused: " + trg.Group + " " + trg.Name))
            override this.TriggersPaused(trg) = log.Write(Debug("TriggersPaused: " + trg))
            override this.TriggerResumed(trg) = log.Write(Debug("TriggerResumed: " + trg.Group + " " + trg.Name))
            override this.TriggersResumed(trg) = log.Write(Debug("TriggersResumed: " + trg))
            override this.SchedulerError(msg, ex) = log.Write(Error("SchedulerError: " + msg,ex,false))
            override this.SchedulerInStandbyMode() = log.Write(Info("SchedulerInStandbyMode: "))
            override this.SchedulerStarted() = log.Write(Info("SchedulerStarted: "))
            override this.SchedulerStarting() = log.Write(Info("SchedulerStarting: "))
            override this.SchedulerShutdown() = log.Write(Info("SchedulerShutdown: "))
            override this.SchedulerShuttingdown() = log.Write(Info("SchedulerShuttingdown: "))
            override this.SchedulingDataCleared() = log.Write(Info("SchedulingDataCleared: "))
           
            

    type JobScheduler(log:ILogWriter, bus:IBus, scheduler:IScheduler) =        
       
        do log.Write(Info("-- Scheduler started --"))   
        let mutable triggerBuilder = TriggerBuilder.Create().WithCalendarIntervalSchedule(fun a-> (a.WithInterval(1, IntervalUnit.Minute) |> ignore))
        let handleBatch (batch:Batch) (trigger:ITrigger) (tp:'a) =      let jobDetail = new JobDetailImpl(batch.Name,  batch.BatchId.ToString(), tp)
                                                                        let found = scheduler.GetJobDetail(jobDetail.Key)
                                                                        match found with 
                                                                            | null -> scheduler.ScheduleJob(jobDetail, trigger) |> ignore
                                                                            | _ -> scheduler.RescheduleJob(new TriggerKey(trigger.Key.Name), trigger) |> ignore
                                                                        let dto = trigger.GetNextFireTimeUtc()
                                                                        match dto.HasValue with
                                                                        | true -> log.Write(Info(jobDetail.Name + " next fire time (local) " + dto.Value.ToLocalTime().ToString()))
                                                                        | false -> log.Write(Info(jobDetail.Name + " not scheduled triggers" ))
        interface IJobScheduler with 
                override this.createTrigger with get() = triggerBuilder  and set(v) = triggerBuilder <- v           
                override this.scheduleBatch<'a when 'a :> IJob>(batch:Lacjam.Core.Batch, trgBuilder:TriggerBuilder) =   trgBuilder.WithIdentity(batch.Name + " " + batch.BatchId.ToString()) |> ignore
                                                                                                                        let trig = trgBuilder.WithIdentity(batch.Name + "  " + batch.BatchId.ToString()).Build()
                                                                                                                        handleBatch batch trig typedefof<'a>
                                                                
                override this.scheduleBatch<'a when 'a :> IJob>(batch:Lacjam.Core.Batch) = 
                                                                                                                        let triggerBuilder = match batch.TriggerBuilder with | null -> triggerBuilder | _ -> batch.TriggerBuilder
                                                                                                                        let trig = triggerBuilder.WithIdentity(batch.Name + "  " + batch.BatchId.ToString()).Build()
                                                                                                                        handleBatch batch trig typedefof<'a>
                                                           
                
                member this.processBatch(batch) =   
                                                    let agent = MailboxProcessor<BatchMessage>.Start(fun proc -> 
                                                                                                            let rec loop n =
                                                                                                                async {
                                                                                                                        try
                                                                                                                                            let! (log, bus, jobMessage, replyChannel) = proc.Receive()
                                                                                                                                            log.Write(Debug("Sending -- " + jobMessage.GetType().Name))
                                                                                                                                            bus.Send(jobMessage).Register(fun (a:CompletionResult) ->           
                                                                                                                                                                                                        match a with
                                                                                                                                                                                                            | null ->   let msg = "No Completion Result reply for NServiceBus Job"
                                                                                                                                                                                                                        log.Write(Info(msg))  
                                                                                                                                                                                                                        replyChannel.Reply(new Jobs.JobResult(jobMessage,false, msg))   
                                                                                                                                                                                                            | _     ->
                                                                                                                                                                                                                
                                                                                                                                                                                
                                                                                                                                                                                                                try
                                                                                                                                                                                                                            if (a.ErrorCode > 0 ) then
                                                                                                                                                                                                                                log.Write(Info("ErrorCode-returned: " + a.ErrorCode.ToString()))  

                                                                                                                                                                                                                            match a.Messages.FirstOrDefault() with
                                                                                                                                                                                                                            | null ->  
                                                                                                                                                                                                                                    log.Write(Debug("JobResult -- not returned messages for JobResult")) 
                                                                                                                                                                                                                                    log.Write(Debug("Async State -- " + a.State.ToString()))
                                                                                                                                                                                                                                    replyChannel.Reply(new Jobs.JobResult(jobMessage,true, "No messages results")) 
                                                                                                                                                                                                
                                                                                                                                                                                                                            | b ->
                                                                                                                                                                                                                                    let jr = (b :?> Jobs.JobResult)
                                                                                                                                                                                                                                    log.Write(Debug("JobResult -- " + jr.GetType().Name))
                                                                                                                                                                                                                                    log.Write(Debug("JobResult -- " + jr.ToString())) 
                                                                                                                                                                                                                                    //TODO send original job message update
                                                                                                                                                                                                                                    replyChannel.Reply(jr) 
                                                                                                                                                                                                                with | ex -> log.Write(Error("Job failed", ex, false))
                                                                                                                                                                                                                             replyChannel.Reply(new Jobs.JobResult(jobMessage, false, "Error: " + ex.Message))   
                                                                                                                                                                           )  |> ignore
                                                                                                                                                                           
                                                                                                                                          with | ex -> log.Write(Error("Job failed", ex, false))
                                                                                                                        do! loop (n + 1)
                                                                                                                }
                                                                                                            loop 0)
                                                                                                
                                                    let mutable payload = batch.Jobs.FirstOrDefault().Payload
                                                    for job in batch.Jobs do
                                                        try
                                                            job.Payload <- payload
                                                            let reply = agent.PostAndReply(fun replyChannel -> log, bus, job, replyChannel)
                                                            payload <- reply.Result
                                                            log.Write(Info("Reply: %s" + reply.ToString()))
                                                        with | ex -> log.Write(Error("Job failed", ex, false))
                                                    
                                                    ()
                
                member this.Scheduler =  scheduler
      
      type ProcessBatch() =
        interface IJob with
            member this.Execute(context : IJobExecutionContext)  =      let log = Lacjam.Core.Runtime.Ioc.Resolve<ILogWriter>()
                                                                        let bus = Lacjam.Core.Runtime.Ioc.Resolve<IBus>()
                                                                        let batchName = context.JobDetail.Key.Name
                                                                        Console.WriteLine(batchName)
                                                                        Console.WriteLine("Job is executing - {0}.", DateTime.Now)
                                                                        try
                                                                            let js = Ioc.Resolve<IJobScheduler>()
                                                                            let asses = AppDomain.CurrentDomain.GetAssemblies().Where(fun a-> a.FullName.Contains("Lacjam"))
                                                                            for ass in asses do
                                                                                let types = ass.GetTypes()
                                                                                for ty in types do
                                                                                    let cb = ty.GetInterface(typedefof<IContainBatches>.FullName)
                                                                                    match cb with
                                                                                        | null -> ()
                                                                                        | _ -> 
                                                                                                let batches = Activator.CreateInstance(ty) :?> IContainBatches
                                                                                                let b = batches.Batches.Head
                                                                                                js.processBatch(b) 
                                                                                                context.Result <- true 
                                                                               
                                                                        with | ex -> log.Write(Error("Job failed", ex, false)) 
             
     type BatchSubmitterJobHandler(log : Lacjam.Core.Runtime.ILogWriter, js : IJobScheduler) =
            do log.Write(Info("BatchSubmitterJob"))
            interface NServiceBus.IHandleMessages<BatchSubmitterJob> with
                member x.Handle(job) =
                    try
                        log.Write(LogMessage.Info("Handling Batch  : " + job.ToString()))
                        log.Write(Info("EndpointConfig.Init :: SchedulerName = " + js.Scheduler.SchedulerName))
                        log.Write(Info("EndpointConfig.Init :: IsStarted = " + js.Scheduler.IsStarted.ToString()))
                        log.Write(Info("EndpointConfig.Init :: SchedulerInstanceId = " + js.Scheduler.SchedulerInstanceId.ToString()))
                        js.scheduleBatch<ProcessBatch>(job.Batch)
                    with ex ->
                        log.Write(LogMessage.Error(job.ToString(), ex, true))        

    
    let callBackReceiver (result:CompletionResult) = 
            Console.WriteLine("--- CALLBACK ---")
            // TODO Audit

   
        
  