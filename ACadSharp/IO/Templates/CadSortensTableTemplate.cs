using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using System.Collections.Generic;

namespace ACadSharp.IO.Templates
{
	internal class CadSortensTableTemplate : CadTemplate<SortEntitiesTable>
	{
		public ulong? BlockOwnerHandle { get; set; }

        public List<KeyValuePair<ulong?, ulong?>> Values { get; } = new List<KeyValuePair<ulong?, ulong?>>();

        public CadSortensTableTemplate() : base(new SortEntitiesTable()) { }

		public CadSortensTableTemplate(SortEntitiesTable cadObject) : base(cadObject) { }

		public override void Build(CadDocumentBuilder builder)
		{
			base.Build(builder);

			if (builder.TryGetCadObject(this.BlockOwnerHandle, out CadObject owner))
			{
				// Not always a block
				if (owner is BlockRecord record)
				{
					this.CadObject.BlockOwner = record;
				}
				else if (owner is null)
				{
					builder.Notify($"Block owner for SortEntitiesTable {this.CadObject.Handle} not found", NotificationType.Warning);
					return;
				}
				else
				{
					builder.Notify($"Block owner for SortEntitiesTable {this.CadObject.Handle} is not a block {owner.GetType().FullName} | {owner.Handle}", NotificationType.Warning);
					return;
				}
			}

            foreach (KeyValuePair<ulong?, ulong?> pair in this.Values)
            {
                if (pair.Value.HasValue && builder.TryGetCadObject(pair.Value.Value, out Entity entity))
                {
                    this.CadObject.Add(entity, pair.Key.GetValueOrDefault());
                }
                else
                {
                    builder.Notify(
                        $"Entity in SortEntitiesTable {this.CadObject.Handle} not found {pair.Value}",
                        NotificationType.Warning
                    );
                }
            }
        }
	}
}
