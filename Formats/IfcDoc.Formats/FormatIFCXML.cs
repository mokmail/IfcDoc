using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuildingSmart.IFC;
using BuildingSmart.IFC.IfcKernel;
using BuildingSmart.Serialization.Xml;
using IfcDoc.Schema.DOC;


namespace IfcDoc.Format.IFCXML
{
	public class FormatIFCXML : IDisposable
	{
		IfcContext root;
		FileStream m_stream;
		DocProject m_project;
		DocSchema m_schema; // optional: only capture specific schema
		DocModelView[] m_views;
		Dictionary<DocObject, bool> m_included;


		public FormatIFCXML(IfcContext dataset, DocProject docProject, DocSchema docSchema, DocModelView[] modelviews, FileStream stream)
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
			XmlHeader header = new XmlHeader();
			XmlElementIfc xmlElementIfc = new XmlElementIfc(header, root);
			XmlSerializer format = new XmlSerializer(root.GetType())
			{
				NameSpace = XmlElementIfc.NameSpace,
				SchemaLocation = XmlElementIfc.SchemaLocation
			};
			format.WriteObject(m_stream, xmlElementIfc);
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
