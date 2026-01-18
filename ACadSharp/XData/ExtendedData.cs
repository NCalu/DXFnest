using ACadSharp.Tables;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.XData
{
	/// <summary>
	/// Extended data linked to an <see cref="AppId"/>.
	/// </summary>
	public class ExtendedData
	{
		/// <summary>
		/// Records contained in this Extended Data instance.
		/// </summary>
		public List<ExtendedDataRecord> Records { get; } = new List<ExtendedDataRecord>();   //Should be private?

		/// <summary>
		/// Default constructor.
		/// </summary>
		public ExtendedData()
		{
		}

		/// <summary>
		/// Initialize an instance of <see cref="ExtendedData"/> with a collection of records.
		/// </summary>
		/// <param name="records">Records to add to the <see cref="ExtendedData"/>.</param>
		public ExtendedData(IEnumerable<ExtendedDataRecord> records) : this()
		{
			this.Records.AddRange(records);
		}

        /// <summary>
        /// Make sure that the first and last record are <see cref="ExtendedDataControlString"/>.
        /// </summary>
        /// <remarks>
        /// The first control string must be the opening one and the last one closing.
        /// </remarks>
        public void AddControlStrings()
        {
            if (!this.Records.Any())
            {
                this.Records.Add(ExtendedDataControlString.Open);
                this.Records.Add(ExtendedDataControlString.Close);
                return;
            }

            var first = this.Records.First();
            ExtendedDataControlString firstControl = first as ExtendedDataControlString;
            if (firstControl == null || firstControl.IsClosing)
            {
                this.Records.Insert(0, ExtendedDataControlString.Open);
            }

            var last = this.Records.Last();
            ExtendedDataControlString lastControl = last as ExtendedDataControlString;
            if (lastControl == null || !lastControl.IsClosing)
            {
                this.Records.Add(ExtendedDataControlString.Close);
            }
        }
    }
}
