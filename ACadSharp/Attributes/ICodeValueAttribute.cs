namespace ACadSharp.Attributes
{
    public interface ICodeValueAttribute
    {
        /// <summary>
        /// Dxf codes binding the property
        /// </summary>
        DxfCode[] ValueCodes { get; }

        /// <summary>
        /// Reference type for this dxf property
        /// </summary>
        DxfReferenceType ReferenceType { get; }
    }
}
