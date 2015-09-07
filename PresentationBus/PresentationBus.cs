using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PresentationBus
{
    public class PresentationBus : IPresentationBus, IPresentationBusConfiguration
    {
        private readonly Dictionary<Type, EventSubscribers> _subscribersByEventType;
        private readonly Dictionary<Type, RequestSubscribers> _subscribersByRequestType;
        private readonly Dictionary<Type, CommandSubscribers> _subscribersByCommandType;

        public PresentationBus()
        {
            _subscribersByEventType = new Dictionary<Type, EventSubscribers>();
            _subscribersByRequestType = new Dictionary<Type, RequestSubscribers>();
            _subscribersByCommandType = new Dictionary<Type, CommandSubscribers>();
        }

        public void Subscribe(IHandlePresentationMessages instance)
        {
            var handlesEvents = instance as IHandlePresentationEvents;
            if (handlesEvents != null)
                Subscribe(handlesEvents);

            var handlesCommands = instance as IHandlePresentationCommands;
            if (handlesCommands != null)
                Subscribe(handlesCommands);

            var handlesRequests = instance as IHandlePresentationRequests;
            if (handlesRequests != null)
                Subscribe(handlesRequests);
        }

        public void UnSubscribe(IHandlePresentationMessages instance)
        {
            var handlesEvents = instance as IHandlePresentationEvents;
            if (handlesEvents != null)
                UnSubscribe(handlesEvents);

            var handlesCommands = instance as IHandlePresentationCommands;
            if (handlesCommands != null)
                UnSubscribe(handlesCommands);

            var handlesRequests = instance as IHandlePresentationRequests;
            if (handlesRequests != null)
                UnSubscribe(handlesRequests);
        }

        private void Subscribe(IHandlePresentationEvents instance)
        {
            ForEachHandledEvent(instance, x => SubscribeForEvents(x, instance));
        }

        private void SubscribeForEvents(Type eventType, object instance)
        {
            EventSubscribers eventSubscribersForEventType;

            if (_subscribersByEventType.ContainsKey(eventType))
            {
                eventSubscribersForEventType = _subscribersByEventType[eventType];
            }
            else
            {
                eventSubscribersForEventType = new EventSubscribers();
                _subscribersByEventType.Add(eventType, eventSubscribersForEventType);
            }

            eventSubscribersForEventType.AddSubscriber(instance);
        }

        private void UnSubscribe(IHandlePresentationEvents instance)
        {
            ForEachHandledEvent(instance, x => UnSubscribeForEvents(x, instance));
        }

        private void UnSubscribeForEvents(Type eventType, object handler)
        {
            if (_subscribersByEventType.ContainsKey(eventType))
            {
                _subscribersByEventType[eventType].RemoveSubscriber(handler);
            }
        }

        private void Subscribe(IHandlePresentationRequests instance)
        {
            ForEachHandledRequest(instance, x => SubscribeForRequests(x, instance));
        }

        private void SubscribeForRequests(Type requestType, object instance)
        {
            RequestSubscribers requestSubscribersForRequestType;

            if (_subscribersByRequestType.ContainsKey(requestType))
            {
                requestSubscribersForRequestType = _subscribersByRequestType[requestType];
            }
            else
            {
                requestSubscribersForRequestType = new RequestSubscribers();
                _subscribersByRequestType.Add(requestType, requestSubscribersForRequestType);
            }

            requestSubscribersForRequestType.AddSubscriber(instance);
        }

        private void UnSubscribe(IHandlePresentationRequests instance)
        {
            ForEachHandledRequest(instance, x => UnSubscribeForRequests(x, instance));
        }

        private void UnSubscribeForRequests(Type requestType, object handler)
        {
            if (_subscribersByRequestType.ContainsKey(requestType))
            {
                _subscribersByRequestType[requestType].RemoveSubscriber(handler);
            }
        }

        public async Task Publish<TEvent>(TEvent presentationEvent) where TEvent : IPresentationEvent
        {
            var type = presentationEvent.GetType();
            var typeInfo = type.GetTypeInfo();
            foreach (var subscribedType in _subscribersByEventType.Keys.Where(t => t.GetTypeInfo().IsAssignableFrom(typeInfo)).ToArray())
            {
                await _subscribersByEventType[subscribedType].Publish(presentationEvent);
            }
        }

        public async Task Send<TCommand>(TCommand presentationEvent) where TCommand : IPresentationCommand
        {
            var type = presentationEvent.GetType();
            var typeInfo = type.GetTypeInfo();
            foreach (var subscribedType in _subscribersByCommandType.Keys.Where(t => t.GetTypeInfo().IsAssignableFrom(typeInfo)).ToArray())
            {
                await _subscribersByCommandType[subscribedType].Send(presentationEvent);
            }
        }

        public async Task<TResponse> Request<TRequest, TResponse>(IPresentationRequest<TRequest, TResponse> request)
            where TRequest : IPresentationRequest<TRequest, TResponse>
            where TResponse : IPresentationResponse
        {
            var type = request.GetType();
            var typeInfo = type.GetTypeInfo();
            var result = default(TResponse);
            foreach (var subscribedType in _subscribersByRequestType.Keys.Where(t => t.GetTypeInfo().IsAssignableFrom(typeInfo)).ToArray())
            {
                var results = await _subscribersByRequestType[subscribedType].PublishRequest<TRequest, TResponse>((TRequest)request);
                result = results.FirstOrDefault();
                if (result != null)
                {
                    return result;
                }
            }
            return result;
        }

        public async Task<IEnumerable<TResponse>> MulticastRequest<TRequest, TResponse>(IPresentationRequest<TRequest, TResponse> request)
            where TRequest : IPresentationRequest<TRequest, TResponse>
            where TResponse : IPresentationResponse
        {
            var type = request.GetType();
            var typeInfo = type.GetTypeInfo();
            var results = new List<TResponse>();
            foreach (var subscribedType in _subscribersByRequestType.Keys.Where(t => t.GetTypeInfo().IsAssignableFrom(typeInfo)).ToArray())
            {
                var result = await _subscribersByRequestType[subscribedType].PublishRequest<TRequest,TResponse>((TRequest)request);
                results.AddRange(result);
            }
            return results;
        }

        private void ForEachHandledEvent(IHandlePresentationEvents instance, Action<Type> callback)
        {
            var handlesEventsType = typeof(IHandlePresentationEvents);

            var type = instance.GetType();

            var interfaceTypes = type.GetTypeInfo().ImplementedInterfaces;
            foreach (var interfaceType in interfaceTypes.Where(x => x.IsConstructedGenericType && handlesEventsType.GetTypeInfo().IsAssignableFrom(x.GetTypeInfo())))
            {
                var eventType = interfaceType.GenericTypeArguments.First();
                callback(eventType);
            }
        }

        private void ForEachHandledRequest(IHandlePresentationRequests instance, Action<Type> callback)
        {
            var handlesRequestsType = typeof(IHandlePresentationRequests);

            var type = instance.GetType();

            var interfaceTypes = type.GetTypeInfo().ImplementedInterfaces;
            foreach (var interfaceType in interfaceTypes.Where(x => x.IsConstructedGenericType && handlesRequestsType.GetTypeInfo().IsAssignableFrom(x.GetTypeInfo())))
            {
                var eventType = interfaceType.GenericTypeArguments.First();
                callback(eventType);
            }
        }

        internal class EventSubscribers
        {
            private readonly List<WeakReference> _subscribers; 

            public EventSubscribers()
            {
                _subscribers = new List<WeakReference>();
            }

            public void AddSubscriber<T>(IHandlePresentationEvent<T> instance) where T : IPresentationEvent
            {
                AddSubscriber((object)instance);
            }
            public void AddSubscriber<T>(IHandlePresentationEventAsync<T> instance) where T : IPresentationEvent
            {
                AddSubscriber((object)instance);
            }
            public void AddSubscriber(object instance)
            {
                if (_subscribers.Any(s => s.Target == instance))
                    return;

                _subscribers.Add(new WeakReference(instance));
            }

            public void RemoveSubscriber<T>(IHandlePresentationEvent<T> instance) where T : IPresentationEvent
            {
                RemoveSubscriber((object)instance);
            }
            public void RemoveSubscriber(object instance)
            {
                var subscriber = _subscribers.SingleOrDefault(s => s.Target == instance);
                if (subscriber != null)
                {
                    _subscribers.Remove(subscriber);
                }
            }

            public async Task<bool> Publish<TEvent>(TEvent presentationEvent) where TEvent : IPresentationEvent
            {
                var anySubscribersStillListening = false;

                foreach (var subscriber in _subscribers.Where(s => s.Target != null))
                {
                    var syncHandler = subscriber.Target as IHandlePresentationEvent<TEvent>;
                    if (syncHandler != null)
                        syncHandler.Handle(presentationEvent);
                        
                    var asyncHandler = subscriber.Target as IHandlePresentationEventAsync<TEvent>;
                    if (asyncHandler != null)
                        await asyncHandler.HandleAsync(presentationEvent);

                    anySubscribersStillListening = true;
                }

                return anySubscribersStillListening;
            }
        }

        internal class RequestSubscribers
        {
            private readonly List<WeakReference> _subscribers; 

            public RequestSubscribers()
            {
                _subscribers = new List<WeakReference>();
            }

            public void AddSubscriber<TRequest, TResponse>(IHandlePresentationRequest<TRequest, TResponse> instance)
                where TRequest : IPresentationRequest<TRequest, TResponse>
                where TResponse : IPresentationResponse
            {
                AddSubscriber((object)instance);
            }
            public void AddSubscriber<TRequest, TResponse>(IHandlePresentationRequestAsync<TRequest, TResponse> instance)
                where TRequest : IPresentationRequest<TRequest, TResponse>
                where TResponse : IPresentationResponse
            {
                AddSubscriber((object)instance);
            }

            public void AddSubscriber(object instance)
            {
                if (_subscribers.Any(s => s.Target == instance))
                    return;

                _subscribers.Add(new WeakReference(instance));
            }

            public void RemoveSubscriber<TRequest, TResponse>(IHandlePresentationRequest<TRequest, TResponse> instance)
                where TRequest : IPresentationRequest<TRequest, TResponse>
                where TResponse : IPresentationResponse
            {
                RemoveSubscriber((object)instance);
            }
            public void RemoveSubscriber(object instance)
            {
                var subscriber = _subscribers.SingleOrDefault(s => s.Target == instance);
                if (subscriber != null)
                {
                    _subscribers.Remove(subscriber);
                }
            }

            public async Task<IEnumerable<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request)
                where TRequest : IPresentationRequest<TRequest, TResponse>
                where TResponse : IPresentationResponse
            {
                var results = new List<TResponse>();

                foreach (var subscriber in _subscribers.Where(s => s.Target != null))
                {
                    var syncHandler = subscriber.Target as IHandlePresentationRequest<TRequest, TResponse>;
                    if (syncHandler != null)
                    {
                        results.Add(syncHandler.Handle(request));
                    }

                    var asyncHandler = subscriber.Target as IHandlePresentationRequestAsync<TRequest, TResponse>;
                    if (asyncHandler != null)
                    {
                        results.Add(await asyncHandler.HandleAsync(request));
                    }
                }

                return results;
            }
        }

        internal class CommandSubscribers
        {
            private readonly List<WeakReference> _subscribers;

            public CommandSubscribers()
            {
                _subscribers = new List<WeakReference>();
            }

            public void AddSubscriber<T>(IHandlePresentationCommand<T> instance) where T : IPresentationCommand
            {
                AddSubscriber((object)instance);
            }
            public void AddSubscriber<T>(IHandlePresentationCommandAsync<T> instance) where T : IPresentationCommand
            {
                AddSubscriber((object)instance);
            }
            public void AddSubscriber(object instance)
            {
                if (_subscribers.Any(s => s.Target == instance))
                    return;

                _subscribers.Add(new WeakReference(instance));
            }

            public void RemoveSubscriber<T>(IHandlePresentationCommand<T> instance) where T : IPresentationCommand
            {
                RemoveSubscriber((object)instance);
            }
            public void RemoveSubscriber(object instance)
            {
                var subscriber = _subscribers.SingleOrDefault(s => s.Target == instance);
                if (subscriber != null)
                {
                    _subscribers.Remove(subscriber);
                }
            }

            public async Task<bool> Send<TEvent>(TEvent presentationEvent) where TEvent : IPresentationCommand
            {
                var anySubscribersStillListening = false;

                foreach (var subscriber in _subscribers.Where(s => s.Target != null))
                {
                    var syncHandler = subscriber.Target as IHandlePresentationCommand<TEvent>;
                    if (syncHandler != null)
                        syncHandler.Handle(presentationEvent);

                    var asyncHandler = subscriber.Target as IHandlePresentationCommandAsync<TEvent>;
                    if (asyncHandler != null)
                        await asyncHandler.HandleAsync(presentationEvent);

                    anySubscribersStillListening = true;
                }

                return anySubscribersStillListening;
            }
        }

    }
}