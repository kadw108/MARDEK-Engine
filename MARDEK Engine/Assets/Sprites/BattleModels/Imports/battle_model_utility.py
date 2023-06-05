"""
Usage:
Expects file DefineSprite5118_Exported.xml to exist in working directory.
This file should be obtained by opening vanilla MARDEK in JPEXS,
exporting DefineSprite5118 as an SWF, then exporting the SWF as an XML file.

Has various functions for operating on the XML file and related stuff.

Based on the similar portrait_xml_importer.py in ../Characters/Portraits
"""

# For renaming SVGs
import os

# For parsing XML file
from lxml import etree as ET

# For changing SVG import settings in .svg.meta files
import re

# For exporting as JSON
import json

# What all the imported SVG shapes are scaled up by during importing and in regards to position changes
# (because Unity MARDEK's 1920x1080 resolution is larger than the vanilla res)
globalScaleFactor = 2.66

"""
Represents a frame of a component of a battle model.
Also stores info of the SVG/DefineShape associated with that frame.

e.g. DefineShape3188 (forest fish tail)
"""
class ComponentShape:
    folder_path = "battleModelShapes"

    def __init__(self, label, shapeNum, frameNum):
        self.label = label
        self.shapeNum = shapeNum
        self.frameNum = frameNum

        # This was for getting the correct TransformMatrix position but it might not be necessary. Possibly remove later.
        # self.bounds = self.get_bounds()

    # TODO: remove?
    def get_bounds(self):
        # Search SVG file for the shapeBounds rect info
        # Faster than searching XML
        try:
            DOMTree = ET.parse(self.get_svg_path())
            SVGRoot = DOMTree.getroot()

            matrix = SVGRoot[0].get("transform")
            matrix_list = [float(i) for i in matrix[7:-1].split(", ")]

            # Multiply by 20 to convert to twips
            # Also multiply xMin and yMin by -1 because that makes the bounds finding algorithm actually work...
            # I'm guessing that the signs get flipped when converting the matrix to SVG, but we're converting it back.'
            xMin = -1 * matrix_list[4] * 20
            yMin = -1 * matrix_list[5] * 20

            # IMPORTANT: THIS DOES NOT GET THE CORRECT xMax / yMax VALUES! BUT IT DOESN'T MATTER RIGHT NOW SINCE WE'RE NOT USING THEM REALLY
            xMax = float(SVGRoot.get("width")[:-2]) * 20
            yMax = float(SVGRoot.get("height")[:-2]) * 20

            return {"xMin": xMin, "yMin": yMin, "xMax": xMax, "yMax": yMax}

        except Exception as e:
            print(e)
            print("ERROR FIND COMPONENTSHAPE BOUND FAILED")
            return {"xMin": -1, "yMin": -1, "xMax": -1, "yMax": -1}

    def get_svg_path(self):
        return os.path.join(self.folder_path, str(self.shapeNum) + ".svg")

    def __str__(self):
        return \
        "ComponentShape" + \
        " frameNum " + str(self.frameNum) + \
        ", label " + str(self.label) + \
        ", path " + str(self.get_svg_path())

    def get_json_dict(self):
        return {
            "shapeNumber": self.shapeNum,
            "label": self.label
        }
        
"""
Represents a component of a battle model.
Each component is a part of the model, e.g. arm, leg, torso
Has multiple ComponentShape frames, each representing an SVG used by
one of this model's iterations

(Since MARDEK creatures are often sprite swaps, one battle model
is more like an animation that can be reused by different creatures)

e.g. DefineSprite3189 (fish tail sprite)
"""
class ComponentSprite:
    def __init__(self, spriteNum, matrix, depth):
        self.spriteNum = spriteNum
        self.matrix = matrix
        self.depth = depth

        self.componentFrames = []
        self.scan_component_frames()

    def scan_component_frames(self):
        items = DOMTree.findall(".//{*}item")
        for item in items:
            if item.get("type") == "DefineSpriteTag" and item.get("spriteId") == str(self.spriteNum):
                subItems = item.findall(".//{*}item")

                label = ""
                shapeNum = -1
                frameCount = 0

                # For all frames in DefineSprite[spriteId]:
                for subItem in subItems:

                    # Label of frame (basically the name)
                    if subItem.get("type") == "FrameLabelTag":
                        label = subItem.get("name")

                    # The PlaceObject2Tag's characterId is the id of the DefineShape placed in this frame
                    elif subItem.get("type") == "PlaceObject2Tag":
                        shapeNum = subItem.get("characterId")

                    # All tags associated with a particular frame will show up before the frame's ShowFrameTag
                    # The ShowFrameTag marks the end of that frame
                    elif subItem.get("type") == "ShowFrameTag":

                        # if not empty 'filler' frame:
                        if shapeNum != -1:
                            self.componentFrames.append(
                                ComponentShape(label, shapeNum, frameCount + 1)
                            )
                            frameCount += 1

                            label = ""
                            shapeNum = -1
                break

        # self.set_matrix_bounds()

    # TODO: remove?
    # This is taken from getCharacterBounds
    # in https://github.com/jindrapetrik/jpexs-decompiler/blob/2ddcf6880c0574a6c6cce92ab3146d8c31801fcf/libsrc/ffdec_lib/src/com/jpexs/decompiler/flash/tags/DefineSpriteTag.java
    def set_matrix_bounds(self):
        minX = float('inf')
        maxX = -1
        minY = float('inf')
        maxY = -1

        for shape in self.componentFrames:
            if (shape.bounds["xMin"] < shape.bounds["xMax"] and shape.bounds["yMin"] < shape.bounds["yMax"]):
                minX = min(minX, shape.bounds["xMin"])
                maxX = max(maxX, shape.bounds["xMax"])
                minY = min(minY, shape.bounds["yMin"])
                maxY = max(maxY, shape.bounds["yMax"])

        self.matrix.bounds = {"xMin": minX, "yMin": minY, "xMax": maxX, "yMax": maxY}

    def __str__(self):
        return \
        "ComponentSprite" + \
        " sprite num: " + str(self.spriteNum) + \
        " component frame len: " + str(len(self.componentFrames)) + \
        " bounds: " + str(self.matrix.bounds) + \
        " matrix (converted): " + str(self.matrix.convert_values())

    def get_json_dict(self):
        return {
            "transformMatrix": self.matrix.convert_values(),
            "spriteNumber": self.spriteNum,
            "depth": self.depth,
            "shapes": [shape.get_json_dict() for shape in self.componentFrames]
        }

"""
Represents a matrix transform associated with a PlaceObject2Tag.
Defines the translation/rotation/scale of a sprite placed as a component of a larger sprite.
"""
class TransformMatrix:
    def __init__(self, matrixElement):
        self.translateX = int(matrixElement.get("translateX"))
        self.translateY = int(matrixElement.get("translateY"))

        if matrixElement.get("hasScale") == "true":
            self.hasScale = True
        elif matrixElement.get("hasScale") == "false":
            self.hasScale = False
        else:
            print("Matrix hasScale has invalid value!")

        if self.hasScale:
            self.scaleX = int(matrixElement.get("scaleX"))
            self.scaleY = int(matrixElement.get("scaleY"))
        else:
            self.scaleX = 0
            self.scaleY = 0

        if matrixElement.get("hasRotate") == "true":
            self.hasRotate = True
        elif matrixElement.get("hasRotate") == "false":
            self.hasRotate = False
        else:
            print("Matrix hasRotate has invalid value!")

        if self.hasRotate:
            self.rotateSkew0 = int(matrixElement.get("rotateSkew0"))
            self.rotateSkew1 = int(matrixElement.get("rotateSkew1"))
        else:
            self.rotateSkew0 = 0
            self.rotateSkew1 = 0

        # xMax/yMax unnecessary (I think?)
        # xMin/yMin needed to calculate converted translateX/Y values
        self.bounds = {"xMin": -1, "yMin": -1, "xMax": -1, "yMax": -1}

    def convert_values(self):
        """
        Formula explanation:
        Divide scale/rotate by 2^16 because JPEXS multiplies exported xml-matrix scale/rotate by 2^16
        for some reason (possibly connected to the fixed point bit value formatting used by SWFs)

        Divide translate by 20 because SWF uses TWIP (1/20th of a pixel) as units
        Multiply by globalScaleFactor to account for shape size increase
        newTranslateY is sign flipped because Flash/Unity have reversed Y axes, I think

        You also need to add the xMin/yMin of the shape's shapeBounds rect for some reason, to the translateX/Y respectively
        EDIT: ACTUALLY I DO NOT THINK THAT IS NECESSARY, REMOVE THE BOUNDS STUFF THEN? TODO
        """

        # TODO: remove?
        # if self.bounds["xMin"] == -1 or self.bounds["yMin"] == -1:
        #    print("WARNING! Trying to convert values for matrix without bounds! You should first call set_matrix_bounds on the associated ComponentSprite.")

        newScaleX = self.scaleX / pow(2, 16)
        newScaleY = self.scaleY / pow(2, 16)

        # The rotateSkews have to be flipped, not sure why, but not flipping them leads to the z-euler angle rotation being reversed
        newRotateSkew0 = - self.rotateSkew0 / pow(2, 16)
        newRotateSkew1 = - self.rotateSkew1 / pow(2, 16)

        newTranslateX = (globalScaleFactor) * (1/20.0) * self.translateX # * (self.translateX + self.bounds["xMin"])
        newTranslateY = -1 * (globalScaleFactor) * (1/20.0) * self.translateY # * (self.translateY + self.bounds["yMin"])

        if self.bounds["xMin"] == float('inf') or self.bounds["yMin"] == float('inf'):
            print("Stopgap for the 2 special sprites - ignore all nonavailable/nonstandard bounds")
            newTranslateX = 0
            newTranslateY = 0

        return {"scaleX": newScaleX, "rotateSkew0": newRotateSkew0, "translateX": newTranslateX,
            "scaleY": newScaleY, "rotateSkew1": newRotateSkew1, "translateY": newTranslateY}

    def __str__(self):
        if (self.hasScale):
            scaleStr = " scale-(" + str(self.scaleX) + ", " + str(self.scaleY) + ")"
        else:
            scaleStr = ""

        if (self.hasRotate):
            rotateStr = " rotate-(" + str(self.rotateSkew0) + ", " + str(self.rotateSkew1) + ")"
        else:
            rotateStr = ""

        return \
        "{" + \
        "position-(" + str(self.translateX) + ", " + str(self.translateY) + ")" + \
        scaleStr + \
        rotateStr + "}"

class ComponentInAnimationFrame:
    def __init__(self, component, depth, transform):
        self.component = component
        self.depth = depth
        self.transform = transform

    def __str__(self):
        return \
        "ComponentInAnimationFrame" + \
        " component: " + str(self.component.spriteNum) + \
        " depth: " + str(self.depth) + \
        " transform: " + str(self.transform)

    def get_json_dict(self):
        return {
            "componentDepth": self.component.depth,
            "transform": self.transform.convert_values()
        }

class AnimationFrame:
    def __init__(self, frameNumber, relativeFrameNumber):
        self.frameNumber = frameNumber
        self.relativeFrameNumber = relativeFrameNumber
        self.listOfComponents = []

    def __str__(self):
       return \
       "AnimationFrame" + \
       " frameNumber: " + str(self.frameNumber) + \
       " num of components: " + str(len(self.listOfComponents))

    def get_json_dict(self):
        return {
            "frameNumber": self.frameNumber,
            "relativeFrameNumber": self.relativeFrameNumber,
            "listOfComponents": [i.get_json_dict() for i in self.listOfComponents]
        }

class Animation:
    def __init__(self, name, startFrame):
        self.name = name
        self.startFrame = startFrame

        self.listOfFrames = []

    def __str__(self):
        return \
        "Animation" + \
        " name: " + self.name + \
        " startFrame: " + str(self.startFrame) + \
        " num of frames: " + str(len(self.listOfFrames))

    def get_json_dict(self):
        return {
            "name": self.name,
            "startFrame": self.startFrame,
            "listOfFrames": [i.get_json_dict() for i in self.listOfFrames]
        }

"""
Represents a frame of DefineSprite5118, the master sprite
containing all battle model info.

Each frame represents one battle model for one creature/playable character.
Alternatively, if modelSpriteNum doesn't exist/is -1, it's a "filler" frame
that just exists to separate different types of models and contains no info.

e.g. DefineSprite3214 (fish model)
"""
class BattleModelFrame:
    def __init__(self):
        self.labels = []
        self.frameNum = -1
        self.modelSpriteNum = -1

        self.modelComponents = []
        self.componentDepthDict = {}
        self.animations = []

    def find_model_components_and_animations(self, DOMTree):
        frameCount = 0

        items = DOMTree.findall(".//{*}item")
        for item in items:
            if item.get("type") == "DefineSpriteTag" and item.get("spriteId") == str(self.modelSpriteNum):
                subItems = item.findall(".//{*}item")

                currAnimation = None
                currAnimFrame = None
                emptyFrame = True
                frameCount = 0

                for subItem in subItems:

                    if subItem.get("type") == "PlaceObject2Tag":

                        emptyFrame = False

                        # The first frame of a battle model will place all the component sprites
                        # Which is what we want to find here
                        if frameCount == 0:
                            component = ComponentSprite(
                                subItem.get("characterId"),
                                TransformMatrix(subItem[0]),
                                subItem.get("depth")
                            )
                            self.componentDepthDict[subItem.get("depth")] = component

                            # DefineSprite2306 is for dynamic status bar stuff and not a part of the model
                            if component.spriteNum != "2306":
                                self.modelComponents.append(component)

                        # On any frame that's not the first frame, a PlaceObject2Tag corresponds to 
                        # the movement for one component for the current frame.
                        # Components are identified by depth.
                        else:
                            # This happens sometimes. Guessing it's just Tobias testing things. Probably.
                            if currAnimation is None:
                                # print("BattleModel", self.modelSpriteNum, "has PlaceObject2Tag without animation on frame", frameCount)
                                pass

                            else:
                                if currAnimFrame is None:
                                    currAnimFrame = AnimationFrame(
                                        relativeFrameNumber = frameCount - currAnimation.startFrame,
                                        frameNumber = frameCount
                                    )

                                # PlaceObject2Tags for morphshapes
                                # (ie the shadows for each model which are always the first placeobject2tag in a frame)
                                # don't have a transform matrix so we ignore them
                                if (len(subItem) > 0):
                                    for element in subItem:
                                        # TODO: There is also the colorTransform object, type="CXFORMWITHALPHA"
                                        # which sometimes appears with/instead of MATRIX.
                                        # Should implement that at some point.
                                        if element.get("type") == "MATRIX":

                                            # Sometimes new objects appear in the middle of an animation - this is PlaceObject and RemoveObject stuff
                                            # When an object with depth not in the dict appears it means it's not part of the normal sprite
                                            # eg. it's Mardek's sword swing or something
                                            # TODO add support for this (!!!)
                                            if subItem.get("depth") not in self.componentDepthDict:
                                                # print("WARNING:", subItem.get("depth"), "not in depth dict! Battle Model:", str(self), "Frame:", frameCount, "Subitem:", subItem.items())
                                                pass
                                            else:
                                                cMove = ComponentInAnimationFrame(
                                                    component = self.componentDepthDict[subItem.get("depth")],
                                                    depth = subItem.get("depth"),
                                                    transform = TransformMatrix(subItem[0])
                                                )
                                                currAnimFrame.listOfComponents.append(cMove)

                    elif subItem.get("type") == "FrameLabelTag":
                        emptyFrame = False

                        # end current animation and make a new one

                        if currAnimation is not None:
                            self.animations.append(currAnimation)
                            currAnimFrame = None

                        currAnimation = Animation(
                            name = subItem.get("name"),
                            startFrame = frameCount
                        )

                    # After the first frame, it's all animation info
                    elif subItem.get("type") == "ShowFrameTag":

                        # Don't add any animation info for first frame
                        if frameCount == 0:
                            pass
                        else:
                            # cases: end of an animation, within an animation, within a gap
                            # if currAnimation is None, then a new animation hasn't started yet, so do nothing (gap)

                            if currAnimation is not None:
                                # if it's an empty frame and animation is not empty (end of animation), end that animation
                                if emptyFrame:
                                    self.animations.append(currAnimation)

                                    currAnimation = None
                                    currAnimFrame = None

                                # if it's a non-empty frame and animation is not empty (within animation), add frame to animation
                                else:
                                    if currAnimFrame is None:
                                        pass
                                        # Sometimes currAnimFrame is None when emptyFrame is False
                                        # I think this is fine, probably happens with the frame that indicates a new animation
                                        # Which contains a label, so it's not empty, but sometimes contains no actual PlaceObject2Tag elements
                                    else:
                                        currAnimation.listOfFrames.append(currAnimFrame)
                                        currAnimFrame = None

                        frameCount += 1
                        emptyFrame = True

                break

    def get_name(self):
        # Take last element of self.labels, or "" if it's empty (which it sometimes is)
        # self.labels has length 0-2; if it's 2, the first label is a category label we can ignore

        if not self.labels:
            return "NO NAME"

        return self.labels[len(self.labels) - 1]

    def __str__(self):
        return \
        "BattleModelFrame" + \
        " frame num: " + str(self.frameNum) + \
        " name: " + self.get_name() + \
        " model sprite num: " + str(self.modelSpriteNum) + \
        " animations: " + str([(anim.name, len(anim.listOfFrames)) for anim in self.animations]) + \
        "\n"

    def get_json_dict(self):
        return {
            # Take last element of self.labels, or "" if it's empty (which it sometimes is)
            # self.labels has length 0-2; if it's 2, the first label is a category label we can ignore
            "name": self.get_name(),
            "spriteNumber": self.modelSpriteNum,
            "frameNumber": self.frameNum,
            "components": [compo.get_json_dict() for compo in self.modelComponents],
            "animations": [anim.get_json_dict() for anim in self.animations]
            }

"""
Uses lxml.etree.parse on fileName (expected to be an xml file)
The XML file should be obtained by opening vanilla MARDEK in JPEXS,
exporting DefineSprite5118 as an SWF, then exporting the SWF as an XML file.

Returns DOMTree, which is needed by other functions
"""
def getDOMTree(fileName = ""):
    if fileName == "":
        fileName = input("Enter name of xml file: ")
        print("File:", file)

    # for more efficiency: consider using iterparse?
    customParser = ET.XMLParser(huge_tree=True)
    DOMTree = ET.parse(fileName, parser=customParser)

    return DOMTree

"""
Remove all DefineShapes from an exported XML file, since we use exported SVGs instead.
The DefineShapes in the XML are in the wrong format and also make the file
much larger than it needs to be.
"""
def removeShapes(DOMTree, newFileName):
    items = DOMTree.findall(".//{*}item")
    for item in items:
        if item.get("type") == "DefineShapeTag":
            item.getparent().remove(item)

    DOMTree.write(newFileName)

"""
Reads the frames and framelabels in the DOMTree generated from getDOMTree
Returns a list of frames in DefineSprite5118 (the master battle model sprite)
Each frame is formatted as a BattleModelFrame object
"""
def readFrames(DOMTree):
    frameCount = 0
    frameList = []
    spriteNumberList = []

    # Find the actual DefineSprite in the greater XML representing target DefineSprite5118
    # There is no method for finding node by attribute, so iterate over them
    items = DOMTree.findall(".//{*}item")
    for item in items:
        if item.get("type") == "DefineSpriteTag" and item.get("spriteId") == "5118":

            # Get all the items in the DefineSprite that are frames, and their labels

            # All tags associated with a particular frame will show up before the frame's ShowFrameTag
            # The ShowFrameTag marks the end of that frame, basically
            subItems = item.findall(".//{*}item")
            frameInfo = BattleModelFrame()

            for subItem in subItems:

                # Label of frame (basically the name)
                if subItem.get("type") == "FrameLabelTag":
                    frameInfo.labels.append(subItem.get("name"))

                # Each frame with a battle model in it has one PlaceObject2Tag
                # for the actual battle model's sprite, with name "mdl"
                elif subItem.get("type") == "PlaceObject2Tag" and subItem.get("name") == "mdl":
                    frameInfo.modelSpriteNum = subItem.get("characterId")

                elif subItem.get("type") == "ShowFrameTag":
                    # Different creatures can have the same model, so spriteNumberList is used to avoid repeats
                    if frameInfo.modelSpriteNum != -1 and \
                    frameInfo.modelSpriteNum not in spriteNumberList:
                        frameInfo.frameNum = frameCount + 1
                        frameList.append(frameInfo)
                        spriteNumberList.append(frameInfo.modelSpriteNum)

                    frameCount += 1
                    frameInfo = BattleModelFrame()
            break

    return frameList

"""
Unity's SVG importer does not support SVGs exported by JPEXS through a DefineSprite and not a DefineShape
Because it uses `use data-characterId` and/or `xlink:href="#shapeX"` (?)
Therefore, we edit all the SVGs and replace each `use` element with the shape definition it's referencing
"""
def reformatSVGs(folderName = ""):
    if folderName == "":
        folderName = input("Enter name of folder containing exported SVGs: ")
        print("Folder name:", folderName)

    svgs = [file for file in sorted(os.listdir(folderName)) if file.endswith(".svg")]
    for svg in svgs:
        print("File:", svg)

        svgName = os.path.join(folderName, svg) 
        DOMTree = ET.parse(svgName)
        SVGRoot = DOMTree.getroot()
        shapes = []

        # Find definition of each shape in <defs> section
        defs_list = SVGRoot.findall(".//{*}defs")
        if (len(defs_list) > 1):
            print("WARNING - more than 1 <defs>!")
        elif (len(defs_list) == 0):
            print("WARNING - no <defs>!")
        else:
            defs = defs_list[0]

            for g in defs.findall(".//{*}g"):
                print("g id:", g.get("id"))
                if g.get("id").startswith("shape"):
                    shapes.append(g)

            """
            We want to get rid of <use> but keep its attributes,
            so we turn the <use> element into a <g> element and make each shape
            a child of the new <g>
            """
            use_list = SVGRoot.findall(".//{*}g")[0].findall(".//{*}use")
            if (len(use_list) == 0):
                print("WARNING: No <use> element!")
            else:
                for i in range(len(shapes)):
                    use_element = use_list[i]
                    use_element.tag = "g"
                    use_element.append(shapes[i])
                    #print("Shape added:", shapes[i].get("id"))

            DOMTree.write(svgName)

    print("SVGs in", folderName, "formatted.")

"""
Set SVG import settings for all .svg.meta files in folderName.

The SVG is expected to have "Textured Sprite" as its Generated Asset Type.

TextureSize is max(width, height) * scaleFactor
scaleFactor was determined experimentally.
"""
def svg_import_settings(folderName = "", scaleFactor = globalScaleFactor):
    if folderName == "":
        folderName = input("Enter name of folder containing exported SVGs: ")
        print("Folder name:", folderName)

    for metadata in [f for f in sorted(os.listdir(folderName)) if f.endswith(".svg.meta")]:

        metadata = os.path.join(folderName, metadata)

        # get texture size of imported SVG
        svgName = metadata[:-5]
        DOMTree = ET.parse(svgName)
        SVGRoot = DOMTree.getroot()
        height = float(SVGRoot.get("height")[:-2])
        width = float(SVGRoot.get("width")[:-2])
        textureSize = round(max(height, width) * scaleFactor)
        print("read:", svgName, "height:", height, "width:", width, "textureSize:", textureSize)

        file_text = ""
        with open(metadata, "r") as f:
            file_text = f.read()

            # set Pixels per Unit to 1
            # file_text = re.sub(r'svgPixelsPerUnit: [0-9]+\n', r'svgPixelsPerUnit: 1\n', file_text)

            # set Texture Size to correct value
            file_text = re.sub(r'textureSize: [0-9]+\n', r'textureSize: ' + str(textureSize) + r'\n', file_text)

        with open(metadata, "w") as f:
            f.write(file_text)


def run_all():
    files = [f for f in sorted(os.listdir()) if os.path.isdir(f)]
    for folderName in files:
        print("folder:", folderName)

        fileName = os.path.join(folderName, "swf.xml")
        try:
            frameList = readFrames(fileName)
            renameSVGs(frameList, folderName)
        except:
            print("SWF not detected")

        reformatSVGs(folderName)

if __name__ == "__main__":
    DOMTree = getDOMTree("DefineSprite5118_Exported.xml")
    frames = readFrames(DOMTree)

    model = frames[33]
    model.find_model_components_and_animations(DOMTree)
    print(model)

    with open("test.json", "w") as f:
        json.dump([model.get_json_dict()], f)

    """
    for i in forestFish.modelComponents:
        print(i)
        print([str(j) for j in i.componentFrames])
        print()
    """
