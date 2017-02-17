﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CQRSalad.Dispatching
{
    internal delegate object MessageInvoker(object handler, object message);

    public interface IDispatcherHandlersController
    {
        void Init(IEnumerable<Type> typesToRegister);
    }

    public class DispatcherHandlersController : IDispatcherHandlersController
    {
        private readonly SubscriptionsStore _subscriptionsStore = new SubscriptionsStore();
        protected virtual Priority DefaultPriorty => Priority.Normal;

        //todo: Add validation

        public void Init(IEnumerable<Type> typesToRegister)
        {
            IEnumerable<Type> handlerTypes = typesToRegister
                .Distinct()
                .Where(type => type.IsClass
                               && type.IsPublic
                               && !type.IsAbstract
                               && !type.IsGenericTypeDefinition
                               && !type.ContainsGenericParameters)
                .Where(IsDispatcherHandler);

            foreach (Type handlerType in handlerTypes)
            {
                RegisterHandler(handlerType);
            }
        }

        public virtual bool IsDispatcherHandler(Type type)
        {
            return type.IsDefined(typeof(DispatcherHandlerAttribute));
        }

        private void RegisterHandler(Type handlerType)
        {
            IEnumerable<MethodInfo> actions = GetHandlerActions(handlerType);
            foreach (MethodInfo action in actions)
            {
                Type messageType = action.GetParameters()[0].ParameterType;
                MessageInvoker invoker = CreateInvoker(messageType, handlerType, action);

                var subscription = new Subscription
                {
                    MessageType = messageType,
                    HandlerType = handlerType,
                    Invoker = invoker,
                    Priority = GetDispatchingPriority(handlerType, action)
                };

                _subscriptionsStore.Add(subscription);
            }
        }

        private IEnumerable<MethodInfo> GetHandlerActions(Type handlerType)
        {
            List<MethodInfo> actions = handlerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsHandlerAction)
                .ToList();
            return actions;
        }

        public virtual bool IsHandlerAction(MethodInfo method)
        {
            bool isDefinitionMatch = method.IsPublic &&
                                     method.GetParameters().Length == 1 &&
                                     !method.IsAbstract &&
                                     !method.ContainsGenericParameters &&
                                     !method.IsConstructor &&
                                     !method.IsGenericMethod &&
                                     !method.IsStatic;

            return isDefinitionMatch;
        }

        public virtual Priority GetDispatchingPriority(Type handlerType, MethodInfo action)
        {
            Type attributeType = typeof(DispatchingPriorityAttribute);

            if (Attribute.IsDefined(action, attributeType))
            {
                return action.GetCustomAttribute<DispatchingPriorityAttribute>(false).Priority;
            }

            if (Attribute.IsDefined(handlerType, attributeType))
            {
                return handlerType.GetCustomAttribute<DispatchingPriorityAttribute>(false).Priority;
            }

            return DefaultPriorty;
        }

        private static MessageInvoker CreateInvoker(Type messageType, Type handlerType, MethodInfo action)
        {
            Type objectType = typeof(object);
            ParameterExpression handlerParameter = Expression.Parameter(objectType, "handler");
            ParameterExpression messageParameter = Expression.Parameter(objectType, "message");

            MethodCallExpression methodCall =
                Expression.Call(
                    Expression.Convert(handlerParameter, handlerType),
                    action,
                    Expression.Convert(messageParameter, messageType));

            if (action.ReturnType == typeof(void))
            {
                var lambda = Expression.Lambda<Action<object, object>>(
                    methodCall,
                    handlerParameter,
                    messageParameter);

                Action<object, object> voidExecutor = lambda.Compile();
                return (handler, message) =>
                {
                    voidExecutor(handler, message);
                    return null;
                };
            }
            else
            {
                var lambda = Expression.Lambda<MessageInvoker>(
                    Expression.Convert(methodCall, typeof(object)),
                    handlerParameter,
                    messageParameter);

                return lambda.Compile();
            }
        }
    }
}