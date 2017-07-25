// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Document.cs" company="Microsoft Corporation">
//  All Rights Reserved  
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace StructureExtraction
{
    public class Document
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public int Start { get; internal set; }
        public int End { get; internal set; }
    }
}
