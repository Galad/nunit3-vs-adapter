﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace NUnit.VisualStudio.TestAdapter.Navigation
{
#if !NET45
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Reflection.Metadata.Ecma335;

    /// <summary>
    /// The portable pdb reader.
    /// </summary>
    internal class PortablePdbReader : IDisposable
    {
        /// <summary>
        /// Use to get method token 
        /// </summary>
        private static readonly PropertyInfo MethodInfoMethodTokenProperty =
            typeof(MethodInfo).GetProperty("MetadataToken");

        /// <summary>
        /// Metadata reader provider from portable pdb stream
        /// To get Metadate reader
        /// </summary>
        private MetadataReaderProvider provider;

        /// <summary>
        /// Metadata reader from portable pdb stream
        /// To get method debug info from mehthod info
        /// </summary>
        private MetadataReader reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="PortablePdbReader"/> class.
        /// </summary>
        /// <param name="stream">
        /// Portable pdb stream
        /// </param>
        /// <exception cref="Exception">
        /// Raises Exception on given stream is not portable pdb stream
        /// </exception>
        public PortablePdbReader(Stream stream)
        {
            if (!IsPortable(stream))
            {
                throw new Exception("Given stream is not portable stream");
            }

            this.provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            this.reader = this.provider.GetMetadataReader();
        }

        /// <summary>
        /// Dispose Metadata reader
        /// </summary>
        public void Dispose()
        {
            this.provider?.Dispose();
            this.provider = null;
            this.reader = null;
        }

        /// <summary>
        /// Gets dia navigation data from Metadata reader 
        /// </summary>
        /// <param name="methodInfo">
        /// Method info.
        /// </param>
        /// <returns>
        /// The <see cref="DiaNavigationData"/>.
        /// </returns>
        public NavigationData GetDiaNavigationData(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return null;
            }

            var handle = GetMethodDebugInformationHandle(methodInfo);

            return this.GetDiaNavigationData(handle);
        }

        internal static MethodDebugInformationHandle GetMethodDebugInformationHandle(MethodInfo methodInfo)
        {
            var methodToken = (int)MethodInfoMethodTokenProperty.GetValue(methodInfo);
            var handle = ((MethodDefinitionHandle)MetadataTokens.Handle(methodToken)).ToDebugInformationHandle();
            return handle;
        }

        private static void GetMethodMinAndMaxLineNumber(
            MethodDebugInformation methodDebugDefinition,
            out int minLineNumber,
            out int maxLineNumber)
        {
            minLineNumber = int.MaxValue;
            maxLineNumber = int.MinValue;
            var orderedSequencePoints = methodDebugDefinition.GetSequencePoints();
            foreach (var sequencePoint in orderedSequencePoints)
            {
                if (sequencePoint.IsHidden)
                {
                    // Special sequence point with startLine is Magic number 0xFEEFEE
                    // Magic number comes from Potable CodeGen source code
                    continue;
                }
                minLineNumber = Math.Min(minLineNumber, sequencePoint.StartLine);
                maxLineNumber = Math.Max(maxLineNumber, sequencePoint.StartLine);
            }
        }

        /// <summary>
        /// Checks gives stream is from portable pdb or not
        /// </summary>
        /// <param name="stream">
        /// Stream.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private static bool IsPortable(Stream stream)
        {
            // First four bytes should be 'BSJB'
            var result = (stream.ReadByte() == 'B') && (stream.ReadByte() == 'S') && (stream.ReadByte() == 'J')
                         && (stream.ReadByte() == 'B');
            stream.Position = 0;
            return result;
        }

        private NavigationData GetDiaNavigationData(MethodDebugInformationHandle handle)
        {
            if (this.reader == null)
            {
                throw new ObjectDisposedException(nameof(PortablePdbReader));
            }

            NavigationData diaNavigationData = null;
            try
            {
                var methodDebugDefinition = this.reader.GetMethodDebugInformation(handle);
                var fileName = this.GetMethodFileName(methodDebugDefinition);
                int minLineNumber, maxLineNumber;
                GetMethodMinAndMaxLineNumber(methodDebugDefinition, out minLineNumber, out maxLineNumber);

                diaNavigationData = new NavigationData(fileName, minLineNumber, maxLineNumber);
            }
            catch (BadImageFormatException exception)
            {
                Console.Write($"failed to get dia navigation data: {exception.Message}");
            }

            return diaNavigationData;
        }

        private string GetMethodFileName(MethodDebugInformation methodDebugDefinition)
        {
            var fileName = string.Empty;
            if (!methodDebugDefinition.Document.IsNil)
            {
                var document = this.reader.GetDocument(methodDebugDefinition.Document);
                fileName = this.reader.GetString(document.Name);
            }

            return fileName;
        }
    }
#endif
}
