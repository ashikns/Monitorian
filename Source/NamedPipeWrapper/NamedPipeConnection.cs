﻿namespace NamedPipeWrapper
{
    using System;
    using System.Collections.Concurrent;
    using System.IO.Pipes;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using IO;
    using JetBrains.Annotations;
    using Serialization;
    using Threading;

    /// <summary>
    /// Represents a connection between a named pipe client and server.
    /// </summary>
    /// <typeparam name="TRead">Reference type to read from the named pipe</typeparam>
    /// <typeparam name="TWrite">Reference type to write to the named pipe</typeparam>
    [PublicAPI]
    public class NamedPipeConnection<TRead, TWrite>
        where TRead : class
        where TWrite : class
    {
        /// <summary>
        /// Gets the connection's unique identifier.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Gets the connection's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the connection's handle.
        /// </summary>
        public readonly SafeHandle Handle;

        /// <summary>
        /// Gets a value indicating whether the pipe is connected or not.
        /// </summary>
        public bool IsConnected => _streamWrapper.IsConnected;

        /// <summary>
        /// Invoked when the named pipe connection terminates.
        /// </summary>
        public event ConnectionEventHandler<TRead, TWrite> Disconnected;

        /// <summary>
        /// Invoked whenever a message is received from the other end of the pipe.
        /// </summary>
        public event ConnectionMessageEventHandler<TRead, TWrite> ReceiveMessage;

        /// <summary>
        /// Invoked when an exception is thrown during any read/write operation over the named pipe.
        /// </summary>
        public event ConnectionExceptionEventHandler<TRead, TWrite> Error;

        private readonly PipeStreamWrapper<TRead, TWrite> _streamWrapper;

        private readonly BlockingCollection<TWrite> _writeQueue = new BlockingCollection<TWrite>(); // support multithreading when pushing

        private bool _notifiedSucceeded;

        internal NamedPipeConnection(int id, string name, PipeStream serverStream, ICustomSerializer<TRead> serializerRead, ICustomSerializer<TWrite> serializerWrite)
        {
            Id = id;
            Name = name;
            Handle = serverStream.SafePipeHandle;
            _streamWrapper = new PipeStreamWrapper<TRead, TWrite>(serverStream, serializerRead, serializerWrite);
        }

        /// <summary>
        /// Begins reading from and writing to the named pipe on a background thread.
        /// This method returns immediately.
        /// </summary>
        public void Open()
        {
            var readWorker = new Worker();
            readWorker.Succeeded += OnSucceeded;
            readWorker.Error += OnError;
            readWorker.DoWork(ReadPipe);

            var writeWorker = new Worker();
            writeWorker.Succeeded += OnSucceeded;
            writeWorker.Error += OnError;
            writeWorker.DoWork(WritePipe);
        }

        /// <summary>
        /// Adds the specified <paramref name="message"/> to the write queue.
        /// The message will be written to the named pipe by the background thread
        /// at the next available opportunity.
        /// Note: this method is thread-safe: multiple threads might call this method concurrently.
        /// </summary>
        /// <param name="message"></param>
        public void PushMessage(TWrite message) => _writeQueue.Add(message);
		/// <summary>
		/// Marks write queue as complete.
		/// </summary>
		public void Finish() => _writeQueue.CompleteAdding();
        /// <summary>
        /// Closes the named pipe connection and underlying <c>PipeStream</c>.
        /// </summary>
        public void Close() => CloseImpl();
        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        private void CloseImpl()
        {
            _streamWrapper.Close();
            _writeQueue.CompleteAdding();
        }
        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        private void OnSucceeded()
        {
            // Only notify observers once
            if (_notifiedSucceeded)
                return;

            _notifiedSucceeded = true;

            Disconnected?.Invoke(this);
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        /// <param name="exception"></param>
        private void OnError(Exception exception) => Error?.Invoke(this, exception);

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TRead"/> is not marked as serializable.</exception>
        private void ReadPipe()
        {
            while (IsConnected && _streamWrapper.CanRead)
            {
                var obj = _streamWrapper.ReadObject();
                if (obj == null)
                {
                    CloseImpl();
                    return;
                }
                ReceiveMessage?.Invoke(this, obj);
            }
        }

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TWrite"/> is not marked as serializable.</exception>
        private void WritePipe()
        {
            while (IsConnected && _streamWrapper.CanWrite)
            {
                TWrite x;
                try
                {
                    x = _writeQueue.Take();
                }
                catch (InvalidOperationException)
                {
                    // we have marked the queue as finished, so we don't have more to write
                    break;
                }
                catch (OperationCanceledException)
                {
                    // we have marked the queue as finished, so we don't have more to write
                    break;
                }
                _streamWrapper.WriteObject(x);
                _streamWrapper.WaitForPipeDrain();
            }
        }
    }

    internal static class ConnectionFactory
    {
        private static int _lastId;

        public static NamedPipeConnection<TRead, TWrite> CreateConnection<TRead, TWrite>(PipeStream pipeStream, ICustomSerializer<TRead> serializerRead, ICustomSerializer<TWrite> serializerWrite)
            where TRead : class
            where TWrite : class 
        => new NamedPipeConnection<TRead, TWrite>(++_lastId, "Client " + _lastId, pipeStream, serializerRead, serializerWrite);
    }

    /// <summary>
    /// Handles new connections.
    /// </summary>
    /// <param name="connection">The newly established connection</param>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    public delegate void ConnectionEventHandler<TRead, TWrite>(NamedPipeConnection<TRead, TWrite> connection)
        where TRead : class
        where TWrite : class;

    /// <summary>
    /// Handles new connections.
    /// </summary>
    /// <param name="connection">The newly established connection</param>
    /// <param name="explicitClose">True if the connection has been explicitely closed. Can be used to attempt reconnects</param>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    public delegate void ConnectionDisconnectedEventHandler<TRead, TWrite>(NamedPipeConnection<TRead, TWrite> connection, bool explicitClose)
        where TRead : class
        where TWrite : class;

    /// <summary>
    /// Handles messages received from a named pipe.
    /// </summary>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    /// <param name="connection">Connection that received the message</param>
    /// <param name="message">Message sent by the other end of the pipe</param>
    public delegate void ConnectionMessageEventHandler<TRead, TWrite>(NamedPipeConnection<TRead, TWrite> connection, TRead message)
        where TRead : class
        where TWrite : class;

    /// <summary>
    /// Handles exceptions thrown during read/write operations.
    /// </summary>
    /// <typeparam name="TRead">Reference type</typeparam>
    /// <typeparam name="TWrite">Reference type</typeparam>
    /// <param name="connection">Connection that threw the exception</param>
    /// <param name="exception">The exception that was thrown</param>
    public delegate void ConnectionExceptionEventHandler<TRead, TWrite>(NamedPipeConnection<TRead, TWrite> connection, Exception exception)
        where TRead : class
        where TWrite : class;
}
