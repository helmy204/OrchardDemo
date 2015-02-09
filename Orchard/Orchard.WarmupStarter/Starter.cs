﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Orchard.WarmupStarter
{
    public class Starter<T> where T : class
    {
        private readonly Func<HttpApplication, T> _initialization;
        private readonly Action<HttpApplication, T> _beginRequest;
        private readonly Action<HttpApplication, T> _endRequest;
        private readonly object _synLock = new object();
        /// <summary>
        /// The result of the initialization queued work item.
        /// Set only when initialization has completed without errors.
        /// </summary>
        private volatile T _initializationResult;
        /// <summary>
        /// The (potential) error raised by the initialization thread. This is a "one-time"
        /// error signal, so that we can restart the initialization once another request
        /// comes in.
        /// </summary>
        private volatile Exception _error;
        /// <summary>
        /// The (potential) error from the previous initiazalition. We need to
        /// keep this error active until the next initialization is finished,
        /// so that we can keep reporting the error for all incoming requests.
        /// </summary>
        private volatile Exception _previousError;

        public Starter(Func<HttpApplication,T> initialization,Action<HttpApplication,T> beginRequest,Action<HttpApplication,T> endRequest)
        {
            _initialization = initialization;
            _beginRequest = beginRequest;
            _endRequest = endRequest;
        }

        public void OnBeginRequest(HttpApplication application)
        {

        }

        public void OnApplicationStart(HttpApplication application)
        {
            LaunchStartupThread(application);
        }

        /// <summary>
        /// Run the initialization delegate asynchronously in a queued work item
        /// </summary>
        public void LaunchStartupThread(HttpApplication application)
        {
            // Make sure incoming requests are queued
            WarmupHttpModule.SignalWarmupStart();

            ThreadPool.QueueUserWorkItem(
                state =>
                {
                    try
                    {
                        var result = _initialization(application);
                        _initializationResult = result;
                    }
                    catch (Exception e)
                    {
                        lock (_synLock)
                        {
                            _error = e;
                            _previousError = null;
                        }
                    }
                    finally
                    {
                        // Execute pending requests as the initialization is over
                        WarmupHttpModule.SignalWarmupDone();
                    }
                });
        }

    }
}
