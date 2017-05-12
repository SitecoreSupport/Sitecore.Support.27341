namespace Sitecore.Support.Publishing.Service.Events
{
  using Eventing;
  using Eventing.Remote;
  using Framework.Publishing.Eventing.Remote;
  using Framework.Publishing.PublishJobQueue;
  using Sitecore.Abstractions;
  using Sitecore.Events;
  using Sitecore.Publishing;
  using Sitecore.Publishing.Service.Events;
  using Sitecore.Publishing.Service.SitecoreAbstractions;
  using System;
  using System.Collections.Generic;
  using System.Linq;

  public class PublishEndHandler : Sitecore.Publishing.Service.Events.PublishEventHandlerBase
  {

    private readonly PublishingLogWrapper _logger;

    public PublishEndHandler() : base(new EventWrapper(), new DatabaseFactoryWrapper(new PublishingLogWrapper()))
    {
      this._logger = new PublishingLogWrapper();
    }

    public PublishEndHandler(IEvent eventing, IDatabaseFactory factory) : base(eventing, factory)
    {
      this._logger = new PublishingLogWrapper();
    }


    public void TriggerPublishEnd(object sender, EventArgs args)
    {
      SitecoreEventArgs args2 = args as SitecoreEventArgs;
      if (((args2 != null) && (args2.Parameters != null)) && args2.Parameters.Any<object>())
      {
        PublishingJobEndEventArgs args3 = args2.Parameters[0] as PublishingJobEndEventArgs;
        if ((args3 != null) && (args3.EventData != null))
        {
          PublishingJobEndEvent eventData = args3.EventData;
          List<Sitecore.Publishing.PublishOptions> list = new List<Sitecore.Publishing.PublishOptions>();
          foreach (PublishingJobTargetMetadata metadata in eventData.Targets)
          {
            Publisher publisher = base.BuildPublisher(eventData, metadata.TargetDatabaseName, metadata.TargetId);
            if (!metadata.Succeeded)
            {
              base._eventing.RaiseEvent("publish:fail", new object[] { publisher, new Exception("Publish job for target: " + metadata.TargetName + " (Database:" + metadata.TargetDatabaseName + ") failed. See Publishing Service log for more information.") });
            }
            else
            {
              list.Add(publisher.Options);
              this._logger.Info("Raising : 'publish:end' for '" + metadata.TargetName + "'", null);
              base._eventing.RaiseEvent("publish:end", new object[] { publisher });
              publisher.Options.TargetDatabase.RemoteEvents.Queue.QueueEvent<PublishEndRemoteEvent>(new PublishEndRemoteEvent(publisher), true, true);
            }
          }
          bool failed = eventData.Status == PublishJobStatus.Complete;
          IEnumerable<DistributedPublishOptions> options = from option in list select new DistributedPublishOptions(option);
          this._logger.Info("Raising : 'publish:complete'", null);
          Event.RaiseEvent("publish:complete", new object[] { options, 0, failed });
          EventManager.QueueEvent<PublishCompletedRemoteEvent>(new PublishCompletedRemoteEvent(options, 0L, failed));
          if (!failed && (eventData.Targets.Length == 0))
          {
            object[] parameters = new object[2];
            parameters[1] = new Exception("Publish job : " + eventData.JobId + " failed. No Targets found. See Publishing Service log for more information.");
            base._eventing.RaiseEvent("publish:fail", parameters);
          }
        }
      }
    }
  }
}
