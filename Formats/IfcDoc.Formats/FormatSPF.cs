using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using BuildingSmart.IFC.IfcKernel;
using BuildingSmart.Serialization.Step;
using IfcDoc.Schema.DOC;

namespace IfcDoc.Format.SPF
{
	public class FormatSPF : IDisposable
	{
		object root;
		FileStream m_stream;
		DocProject m_project;
		DocSchema m_schema; // optional: only capture specific schema
		DocModelView[] m_views;
		Dictionary<DocObject, bool> m_included;


		public FormatSPF(object dataset, DocProject docProject, DocSchema docSchema, DocModelView[] modelviews, FileStream stream)
		{
			m_project = docProject;
			m_schema = docSchema;
			m_views = modelviews;
			m_stream = stream;
			root = dataset;

			this.m_included = null;
			if (this.m_views != null)
			{
				this.m_included = new Dictionary<DocObject, bool>();
				foreach (DocModelView docView in this.m_views)
				{
					this.m_project.RegisterObjectsInScope(docView, this.m_included);
				}
			}
		}

		public void Save()
		{
			var assembly = Assembly.GetCallingAssembly().GetName();
			StepSerializer format = new StepSerializer(root.GetType(), null, m_project.GetSchemaIdentifier(), m_project.GetSchemaVersion(), assembly.Name + " " + assembly.Version);//typeof(DocProject).Assembly.GetName().Version);
			format.WriteObject(m_stream, root);
		}

		public void Dispose()
		{
			if (this.m_stream != null)
			{
				this.m_stream.Close();
				this.m_stream = null;
			}
		}
	}
}
