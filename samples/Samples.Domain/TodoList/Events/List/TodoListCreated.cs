﻿using CQRSalad.EventSourcing;

namespace Samples.Domain.TodoList
{
    public sealed class TodoListCreated : IEvent
    {
        public string ListId { get; set; }
        public string Title { get; set; }
        public string OwnerId { get; set; }
    }
}