// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Document.cs" company="Microsoft Corporation">
//  All Rights Reserved  
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace StructureExtraction
{
    using System.Collections.Generic;

    public class Document
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public IDictionary<string, string> Fields { get; } = new Dictionary<string, string>();
    }
}
