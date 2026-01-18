using ACadSharp.Entities;
using System;
using System.Collections;

namespace ACadSharp
{
	public interface ISeqendCollection : IEnumerable
	{
		event EventHandler<CollectionChangedEventArgs> OnSeqendAdded;

		event EventHandler<CollectionChangedEventArgs> OnSeqendRemoved;

		Seqend Seqend { get; }
	}
}
