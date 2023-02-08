﻿namespace Elektrifikatsiya.Models
{
	public class Event
	{
		public string EventName { get; set; }
		public string Description { get; set; }
		public DateTime Date { get; set; }

		//if Plug Class implemented -> Property for which plug this event is from

		public Event(string eventName, string description, DateTime date)
		{
			EventName = eventName;
			Description = description;
			Date = date;
		}
	}
}