namespace ACadSharp.IO.Templates
{
	internal interface ICadDictionaryTemplate : ICadObjectTemplate
	{
		CadObject CadObject { get; set; }
	}
}
