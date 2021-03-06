// 
// BaseHtmlEditorExtension.cs
// 
// Author:
//   Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;
using System.Collections.Generic;

using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.AspNet.Html.Parser;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Dom;
using System.Threading.Tasks;
using System.Threading;

namespace MonoDevelop.AspNet.Html
{
	
	public abstract class BaseHtmlEditorExtension : MonoDevelop.Xml.Editor.BaseXmlEditorExtension
	{
		HtmlSchema schema;
		bool resolvedDocType;

		public override string CompletionLanguage {
			get { return "Html"; }
		}
		
		protected HtmlSchema Schema {
			get {
				if (resolvedDocType)
					return schema;
				
				resolvedDocType = true;
				
				if (DocType == null || String.IsNullOrEmpty (DocType.PublicFpi)) {
					LoggingService.LogDebug ("HTML completion found no doctype, using default");
					schema = HtmlSchemaService.DefaultDocType;
					return schema;
				}
				
				schema = HtmlSchemaService.GetSchema (DocType.PublicFpi, true);
				if (schema != null) {
					LoggingService.LogDebug ("HTML completion using doctype {0}", schema.Name);
				} else {
					LoggingService.LogDebug ("HTML completion could not find schema for doctype {0} so is falling back to default", DocType);
					schema = HtmlSchemaService.DefaultDocType;
				}
				return schema;
			}
		}

		protected override void OnDocTypeChanged ()
		{
			resolvedDocType = false;
		}
		
		#region Setup and teardown
		
		protected override XmlRootState CreateRootState ()
		{
			return new XmlRootState (new HtmlTagState (), new HtmlClosingTagState (true));
		}
		
		protected override void Initialize ()
		{
			base.Initialize ();
			
			//ensure that the schema service is initialised, or code completion may take a couple of seconds to trigger
			HtmlSchemaService.Initialise ();
		}
		
		#endregion
		
		protected override async Task<CompletionDataList> GetElementCompletions (CancellationToken token)
		{
			var list = new CompletionDataList ();
			XName parentName = GetParentElementName (0);
			await AddHtmlTagCompletionData (list, Schema, parentName, token);
			AddMiscBeginTags (list);
			
			//FIXME: don't show this after any elements
			if (DocType == null)
				list.Add ("!DOCTYPE", "md-literal", GettextCatalog.GetString ("Document type"));
			return list;
		}
		
		protected override Task<CompletionDataList> GetDocTypeCompletions (CancellationToken token)
		{
			return Task.FromResult (new CompletionDataList (from DocTypeCompletionData dat
			                               in HtmlSchemaService.DocTypeCompletionData
			                                                select (CompletionData) dat));
		}
		
		protected override async Task<CompletionDataList> GetAttributeCompletions (IAttributedXObject attributedOb,
		                                                                     Dictionary<string, string> existingAtts, CancellationToken token)
		{
			var list = new CompletionDataList ();
			if (attributedOb is XElement && !attributedOb.Name.HasPrefix)
				await AddHtmlAttributeCompletionData (list, Schema, attributedOb.Name, existingAtts, token);
			return list;
		}
		
		protected override async Task<CompletionDataList> GetAttributeValueCompletions (IAttributedXObject ob, XAttribute att, CancellationToken token)
		{
			var list = new CompletionDataList ();
			if (ob is XElement && !ob.Name.HasPrefix)
				await AddHtmlAttributeValueCompletionData (list, Schema, ob.Name, att.Name, token);
			return list;
		}
		
		#region HTML data
		
		protected static async Task AddHtmlTagCompletionData (CompletionDataList list, HtmlSchema schema, XName parentName, CancellationToken token)
		{
			if (schema == null)
				return;
			
			if (parentName.IsValid) {
				list.AddRange (await schema.CompletionProvider.GetChildElementCompletionData (parentName.FullName.ToLower (), token));
			} else {
				list.AddRange (await schema.CompletionProvider.GetElementCompletionData (token));
			}			
		}
		
		protected async Task AddHtmlAttributeCompletionData (CompletionDataList list, HtmlSchema schema, 
		                                                     XName tagName, Dictionary<string, string> existingAtts, CancellationToken token)
		{
			//add atts only if they're not aready in the tag
			foreach (var datum in await schema.CompletionProvider.GetAttributeCompletionData (tagName.FullName.ToLower (), token))
				if (existingAtts == null || !existingAtts.ContainsKey (datum.DisplayText))
					list.Add (datum);
		}
		
		protected async Task AddHtmlAttributeValueCompletionData (CompletionDataList list, HtmlSchema schema, 
		                                                          XName tagName, XName attributeName, CancellationToken token)
		{
			list.AddRange (await schema.CompletionProvider.GetAttributeValueCompletionData (tagName.FullName, 
			                                                                          attributeName.FullName, token));
		}
		
		#endregion
	}
}
