using System;
using System.Xml;
using System.Linq;

namespace AzDeployLib
{
    public class UploadLogEntry
    {
        public string Version { get; set; }
        public DateTime LocalTime { get; set; }
        public FileVersionList Files { get; set; }

        public XmlNode ToXhtml(XmlDocument doc)
        {
            var template = $@"
                <tr>
                    <td>{LocalTime.ToShortDateString()}</td>
                    <td>{Version}</td>
                    <td>
                        <ul>{string.Join("", Files.Select(fv => $"<li><span>{fv.Filename}</span><span> - </span><span>{fv.Version}</span></li>"))}</ul>
                    </td>
                </tr>";

            XmlDocument temp = new XmlDocument();
            temp.LoadXml(template);            
            return doc.ImportNode(temp.DocumentElement, true);
        }
    }
}
