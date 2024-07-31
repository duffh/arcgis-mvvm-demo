// Copyright 2024 Esri
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;

namespace MauiArcGISMVVM
{
    public class PlaceOfWorshipViewModel
    {
        public PlaceOfWorshipViewModel(int index, ArcGISFeature feature)
        {
            // Set the default property values.
            Name = Religion = Fid = "null";

            // Set the display index and feature.
            ArcGISFeature = feature;
            DisplayIndex = index;

            // Get attribute values from the attributes dictionary. 
            if (feature.Attributes.TryGetValue("fid", out object? fidValue) && fidValue != null && fidValue is long fid) Fid = fid.ToString();
            if (feature.Attributes.TryGetValue("religion", out object? religionValue) && religionValue != null) Religion = (string)religionValue;
            if (feature.Attributes.TryGetValue("name", out object? nameValue) && nameValue != null) Name = (string)nameValue; 

            // Given the feature geometry get a formatted coordinate string.
            FormattedCoordinates = CoordinateFormatter.ToLatitudeLongitude((MapPoint)ArcGISFeature.Geometry!, LatitudeLongitudeFormat.DegreesDecimalMinutes, 2);
        }

        #region Properties
        public ArcGISFeature ArcGISFeature { get; }

        public string Fid { get; }

        public string Religion { get; }

        public int DisplayIndex { get; }

        public string Name { get; }

        public string FormattedCoordinates { get; }
        #endregion
    }
}
