using Il2CppNewtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NameBending
{
    public class TypedField
    {
        public NameBend OwnerComponent;
        public Variation Variation => OwnerComponent.Variation;

        public enum FieldType
        {
            Boolean = 0,
            Number = 1,
            String = 2,
            Color = 3,
            Referential = 4
        }
        public enum FormatType
        {
            None = 0,
            SigFigs = 1,
            Round = 2,
            Hex = 3
        }

        public static ReadOnlyCollection<string> FactoryFields = new List<string>() { "RANDOM", "INSTANCE_RANDOM", "FRAME_PROGRESS", "LOOP_PROGRESS", "FRAME", "TIMER" }.AsReadOnly();
        public bool IsFactory
        {
            get
            {
                string param = StringValue?.Split('|')?[0] ?? string.Empty;
                if (param != null)
                    return FactoryFields.Contains(param) || param.Contains("RANDOM");
                return false;
            }
        }

        public string Identifier;
        public int DefinitionIndex = 0;
        public int InstanceIndexTracker = 0;
        public FieldType CurrentFieldType;
        public FormatType CurrentFormatType;

        public bool BooleanValue;
        public float NumberValue;
        public string StringValue;
        public Color ColorValue;

        public int SigFigs = 2;
        public int Round = 1;
        public int HexDigits = 0;

        float lastOutput;

        public List<FieldInstance> Instances = new();

        public bool IsReferential = false;
        public List<Tuple<string, int>> InternalFieldRefs = new();
        public static int DepthTicker = 0;

        public FieldInstance FindNeighborInstance(FieldInstance fieldInstance, bool onlySetters = true, bool findPrevious = false, bool loop = false)
        {
            return FindNeighborInstance(fieldInstance.FrameIndex, fieldInstance.StartPos, onlySetters, findPrevious, loop);
        }
        public FieldInstance FindNeighborInstance(int frameIndex, int startPos, bool onlySetters = true, bool findPrevious = false, bool loop = false)
        {
            if (Instances == null || Instances.Count == 0)
                return null;
            var ordered = Instances.OrderBy(i => i.FrameIndex).ThenBy(i => i.StartPos).ToList();
            if (onlySetters)
            {
                ordered = ordered.Where(i => i.IsSetter).ToList();
                if (ordered.Count == 0) return null;
            }
            if (!findPrevious)
            {
                foreach (var inst in ordered)
                {
                    if (inst.FrameIndex > frameIndex)
                        return inst;
                }
                return loop ? ordered.First() : null;
            }
            else
            {
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    var inst = ordered[i];
                    if (inst.FrameIndex <= frameIndex)
                        return inst;
                }
                return loop ? ordered.Last() : null;
            }
        }
        public FieldInstance FindInstanceAt(int frameIndex, int startPos, bool onlySetters = true)
        {
            foreach (var inst in Instances)
            {
                if (!onlySetters && inst.FrameIndex == frameIndex && inst.StartPos == startPos) return inst;
                if (onlySetters && inst.SetTo != null && inst.FrameIndex == frameIndex && inst.StartPos == startPos) return inst;
            }
            return null;
        }

        public void SetValue(bool booleanValue)
        {
            if (CurrentFieldType is not FieldType.Boolean)
            {
                Debug.Log($"Failed to set field value for {Identifier}: field does not hold booleans");
                return;
            }
            BooleanValue = booleanValue;
        }

        public void SetValue(float numberValue)
        {
            if (CurrentFieldType is not FieldType.Number)
            {
                Debug.Log($"Failed to set field value for {Identifier}: field does not hold numbers");
                return;
            }
            NumberValue = numberValue;
        }

        public void SetValue(string stringValue)
        {
            if (CurrentFieldType is not FieldType.String)
            {
                Debug.Log($"Failed to set field value for {Identifier}: field does not hold strings");
                return;
            }
            StringValue = stringValue;

            IsReferential = FindInternalRefs();
        }

        public void SetValue(Color colorValue)
        {
            if (CurrentFieldType is not FieldType.Color)
            {
                Debug.Log($"Failed to set field value for {Identifier} ({CurrentFieldType.ToString()}): field does not hold colors");
                return;
            }
            ColorValue = colorValue;
        }

        public void SetValueUntyped(string value)
        {
            // Boolean
            if (value == "true") SetValue(true);
            else if (value == "false") SetValue(false);

            // Number
            else if (float.TryParse(value, out var numberValue)) SetValue(numberValue);
            else if (CurrentFormatType is FormatType.Hex && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
                SetValue(hexValue);

            // Color
            else if (ColorUtility.TryParseHtmlString(value, out var colorValue)) SetValue(colorValue);

            // String
            else SetValue(value);
        }

        public static string LerpValueUntyped(string a, string b, float t, TypedField ownerField)
        {
            // Number
            if (ownerField.HexDigits <= 0)
            {
                if (float.TryParse(a, out var numberValueA) &&
                    float.TryParse(b, out var numberValueB))
                {
                    float lerped = Mathf.Lerp(numberValueA, numberValueB, t);
                    return ownerField.ProcessFormatting(lerped);
                }
            }
            else
            {
                if (int.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int numberValueA) &&
                    int.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int numberValueB))
                {
                    float lerped = Mathf.Lerp(numberValueA, numberValueB, t);
                    return ownerField.ProcessFormatting(lerped);
                }
            }

            // Color
            if (ColorUtility.TryParseHtmlString(a, out var colorValueA) &&
                     ColorUtility.TryParseHtmlString(b, out var colorValueB))
            {
                Color lerped = Color.Lerp(colorValueA, colorValueB, t);
                return HelperFunctions.ToHtmlStringRGB(lerped);
            }

            // Booleans and strings cannot be interpolated
            return a;
        }

        public string GetValueAsString(bool entry, int instanceIndex, int frameIndex = -1)
        {
            if (frameIndex == -1) frameIndex = OwnerComponent.FrameIndex;

            if (!IsFactory)
            {
                switch (CurrentFieldType)
                {
                    case FieldType.Boolean: return BooleanValue.ToString();
                    case FieldType.Number: return ProcessFormatting(NumberValue);
                    case FieldType.Color: return HelperFunctions.ToHtmlStringRGB(ColorValue);
                    default: return ProcessInternalRefs(StringValue, entry, instanceIndex);
                }
            }
            else
            {
                var fieldParams = ProcessInternalRefs(StringValue, entry, instanceIndex).Split('|');
                string param1 = fieldParams[0];
                float output = lastOutput;

                bool doRemap = false;
                bool doDilation = false;

                if (param1.Contains("RANDOM"))
                {
                    int hash = HashCode.Combine(OwnerComponent.Timer);
                    if (param1.Contains("FRAME")) hash = HashCode.Combine(OwnerComponent.LoopCount, frameIndex);
                    if (param1.Contains("INSTANCE")) hash = HashCode.Combine(hash, instanceIndex);

                    System.Random randy = new(hash);
                    output = (float)randy.NextDouble();
                    doRemap = true;
                }

                if (param1 == "FRAME_PROGRESS")
                {
                    output = OwnerComponent.FrameProgress;
                    doRemap = true;
                }

                if (param1 == "LOOP_PROGRESS")
                {
                    output = OwnerComponent.LoopProgress;
                    doRemap = true;
                }

                if (param1 == "FRAME")
                {
                    output = OwnerComponent.FrameIndex;
                    doDilation = true;
                }

                if (param1 == "TIMER")
                {
                    output = OwnerComponent.Timer;
                    doDilation = true;
                }

                // Remap to fit the second and third parameters as a min and max respectively (Field|Min|Max)
                if (doRemap)
                {
                    if (fieldParams.Length == 2)
                    {
                        if (float.TryParse(fieldParams[1], out float param2))
                        {
                            output += param2;
                        }
                    }
                    else if (fieldParams.Length > 2)
                    {
                        if (float.TryParse(fieldParams[1], out float param2))
                        {
                            if (float.TryParse(fieldParams[2], out float param3))
                            {
                                output = Mathf.Lerp(param2, param3, output);
                            }
                        }
                    }
                }

                // Remap with scale and offset (Field|Offset|Scale)
                if (doDilation)
                {
                    if (fieldParams.Length > 2)
                    {
                        if (float.TryParse(fieldParams[2], out float param3))
                        {
                            output *= param3;
                        }
                    }
                    if (fieldParams.Length > 1)
                    {
                        if (float.TryParse(fieldParams[1], out float param2))
                        {
                            output += param2;
                        }
                    }
                }

                lastOutput = output;
                return ProcessFormatting(output);
            }
        }

        public string ProcessFormatting(float input)
        {
            if (CurrentFormatType is FormatType.Hex)
            {
                string output = ((int)input).ToString("X");

                if (HexDigits > 0)
                {
                    output = output.Length > HexDigits
                    ? new string('F', HexDigits)
                    : output.PadLeft(HexDigits, '0');
                }

                return output;
            }
            else if (CurrentFormatType is FormatType.SigFigs)
            {
                return input.ToString("n" + SigFigs);
            }
            else if (CurrentFormatType is FormatType.Round)
            {
                if (Round > 0)
                {
                    float rounded = Mathf.Round(input / Round) * Round;
                    return rounded.ToString("n0");
                }
                else
                {
                    CurrentFormatType = FormatType.None;
                }
            }

            return input.ToString("n" + Core.FloatingPointPrecision);
        }

        public bool FindInternalRefs()
        {
            InternalFieldRefs.Clear();
            if (CurrentFieldType is not FieldType.String) return false;

            MatchCollection matches = Regex.Matches(StringValue, Core.FieldPattern);

            foreach (Match match in matches)
            {
                string group1 = match.Groups[1].Value;
                foreach (TypedField typedField in Variation.TypedFields)
                {
                    if (group1 == typedField.Identifier)
                    {
                        InternalFieldRefs.Add(new Tuple<string, int>(typedField.Identifier, match.Index));
                    }
                }
            }

            return InternalFieldRefs.Count > 0;
        }

        public string ProcessInternalRefs(string input, bool entry, int instanceIndex)
        {
            if (entry)
                DepthTicker = 0;

            DepthTicker++;

            string output = String.Empty;
            int writePos = 0;

            if (InternalFieldRefs.Count > 0)
            {
                for (int i = 0; i < InternalFieldRefs.Count; i++)
                {
                    Tuple<string, int> currentFieldRef = InternalFieldRefs[i];

                    string currentValue = String.Empty;
                    if (DepthTicker < Config.FieldDepthLimit.Value)
                    {
                        TypedField referencedField = Variation.FindField(currentFieldRef.Item1);
                        currentValue = referencedField.GetValueAsStringAt(OwnerComponent.FrameIndex, currentFieldRef.Item2, false, instanceIndex);
                    }
                    
                    if (writePos > input.Length) break;
                    output += input.Substring(writePos, currentFieldRef.Item2 - writePos);

                    output += currentValue;
                    writePos = currentFieldRef.Item2 + (currentFieldRef.Item1.Length + 2); // .Length is the identifier length, +2 for the curly braces
                }
                if (writePos <= input.Length) output += input.Substring(writePos);
            }
            else return input;

            return output;
        }

        public string GetValueAsStringAt(FieldInstance fieldInstance, bool entry)
        {
            return GetValueAsStringAt(fieldInstance.FrameIndex, fieldInstance.StartPos, entry, fieldInstance.DefinitionIndex);
        }
        public string GetValueAsStringAt(int frameIndex, int startPos, bool entry, int instanceIndex = -1)
        {
            FieldInstance currentInstance = FindInstanceAt(frameIndex, startPos, true);
            if (currentInstance != null && OwnerComponent.FrameProgress == 0)
                return currentInstance.SetTo;

            FieldInstance nextSetterInstance = FindNeighborInstance(frameIndex, startPos, true, false, Variation.LoopInterpolation);
            FieldInstance prevSetterInstance = FindNeighborInstance(frameIndex, startPos, true, true, Variation.LoopInterpolation);

            if (Variation.Interpolation && nextSetterInstance != null && prevSetterInstance != null)
            {
                string lerpTo = nextSetterInstance.SetTo;
                string lerpFrom = prevSetterInstance.SetTo;

                bool nextLooped = nextSetterInstance.FrameIndex <= prevSetterInstance.FrameIndex;
                bool currentLooped = Variation.OwnerComponent.FrameIndex < prevSetterInstance.FrameIndex;

                int totalFrameCount = Variation.Frames.Count;

                int totalLerpFrames = nextLooped
                    ? (totalFrameCount - prevSetterInstance.FrameIndex) + nextSetterInstance.FrameIndex
                    : nextSetterInstance.FrameIndex - prevSetterInstance.FrameIndex;

                float totalLerpTime = totalLerpFrames * Variation.FrameDuration;

                int lerpedFrames = currentLooped
                    ? (totalFrameCount - prevSetterInstance.FrameIndex) + Variation.OwnerComponent.FrameIndex
                    : Variation.OwnerComponent.FrameIndex - prevSetterInstance.FrameIndex;

                float lerpedTime = (lerpedFrames * Variation.FrameDuration) + (Variation.OwnerComponent.FrameProgress * Variation.FrameDuration);
                float lerpT = lerpedTime / totalLerpTime;

                return TypedField.LerpValueUntyped(lerpFrom, lerpTo, lerpT, nextSetterInstance.OwnerField);
            }

            if ((!Variation.Interpolation || (Variation.Interpolation && nextSetterInstance == null)) && prevSetterInstance != null)
            {
                return prevSetterInstance.SetTo;
            }

            if (instanceIndex == -1) instanceIndex = currentInstance?.DefinitionIndex ?? 0;
            return GetValueAsString(entry, instanceIndex);
        }

        public TypedField(bool booleanValue)
        {
            CurrentFieldType = FieldType.Boolean;
            BooleanValue = booleanValue;
        }

        public TypedField(float numberValue)
        {
            CurrentFieldType = FieldType.Number;
            NumberValue = numberValue;
        }

        public TypedField(string stringValue)
        {
            CurrentFieldType = FieldType.String;
            StringValue = stringValue;
        }

        public TypedField(Color colorValue)
        {
            CurrentFieldType = FieldType.Color;
            ColorValue = colorValue;
        }
    }

    public class FieldInstance
    {
        public TypedField OwnerField;
        public int DefinitionIndex = 0;
        public int FrameIndex = 0;
        public int StartPos = 0;
        public int TotalLength = 0;

        private string _setTo = null;
        public string SetTo
        {
            get
            {
                if (_setTo == null && OwnerField.IsFactory)
                    return OwnerField.GetValueAsString(true, DefinitionIndex, FrameIndex);

                return _setTo;
            }
            set
            {
                _setTo = value;
            }
        }
        public bool IsSetter => !OwnerField.IsFactory && !OwnerField.IsReferential && _setTo != null;

        public FieldInstance(TypedField ownerField, int frameIndex, int startPos, int totalLength)
        {
            OwnerField = ownerField;
            FrameIndex = frameIndex;
            StartPos = startPos;
            TotalLength = totalLength;
            DefinitionIndex = ownerField.DefinitionIndex++;
        }
    }
}
