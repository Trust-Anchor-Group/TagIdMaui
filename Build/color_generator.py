import json
from collections.abc import Generator

"""
Python file that reads a json file and converts the format to be approppriate for xaml in C# MAUI

To use this file, place the JSON file containing colors from Figma named "colors.json" next to this
file (in the same directory) and run the script, it then outputs a "colors.xaml" file.
"""

# Converts hex color from figma to one supported by maui
# Change #XXXXXXAA -> #AAXXXXXX Where A is alpha value
def figma_hex_to_maui(str) -> str:
    if len(str) == 7:
        return str
    elif len(str) == 9:
        return f"#{str[-2:]}{str[1:-2]}"

#Remove Whitespace from names and replace ampersands
def filter_name(str) -> str:
    return "".join(str.split(" ")).replace("&", "").replace("/", "").replace("-", "")

# Gets JSON Object from color list and returns the name and color value.
def json_color_list_to_name_color_pairs(list) -> Generator[str, None, None]:
    for color in list:
        yield (filter_name(color["name"]), figma_hex_to_maui(color["value"]))

# Takes the Name and Color pairs and creates the lines in colors.xaml
def json_color_list_xaml_list(list, dark_mode) -> Generator[str, None, None]:
    for color_name, color_value in json_color_list_to_name_color_pairs(list):
        yield f'\t<Color x:Key="{color_name}{"Dark" if dark_mode else "Light"}">{color_value}</Color>'

xaml_doc_start = """
<?xml version="1.0" encoding="UTF-8" ?>
<?xaml-comp compile="true" ?>
<ResourceDictionary
\txmlns="http://schemas.microsoft.com/dotnet/2021/maui"
\txmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
"""

xaml_doc_end = "</ResourceDictionary>"

def create_xaml_file() -> list[str]:
    with open("colors.json") as f:
        data = json.load(f)[0]["values"]
        formatted_light_colors = list(json_color_list_xaml_list(data[0]["color"], False))
        formatted_dark_colors = list(json_color_list_xaml_list(data[1]["color"], True))

    result = "\n".join([xaml_doc_start] + formatted_light_colors + formatted_dark_colors + [xaml_doc_end])
    
    with open("colors.xaml", "w") as f:
        f.write(result)

    return result

result = create_xaml_file()

print(result)