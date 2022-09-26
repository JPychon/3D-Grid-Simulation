using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace _3D_Grid_Physics
{
    public partial class Grid_3D : Form
    {
        private Device device = null; // Main/parent object used to encapsulate & present the data to the user.
        private VertexBuffer vb = null; // Holds a single vertex data in 3D (x,y,z)
        private IndexBuffer ib = null; // Holds a reference-data set from the vertex buffer to create the indices between them. 
         
        //--------[Grid Size]---------//

        private static int gridWidth = 10; // X axis
        private static int gridLength = 10; // Z axis
        private static int gridHeight = 10; // Y axis

        //---------[Camera Controls]-------//

        private float moveSpeed = 0.2f; // Camera movement speed per click.
        private float turnSpeed = 0.02f; // Camera turning speed per click
        private float rotY = 0; // Rotation on the Y axis. ( 0 < rotY > 2* Math.PI == 360)
        private float tempY = 0; // Buffer to save the inital Y location during mouse-wheel rotation.

        private float rotXZ = 0;
        private float tempXZ = 0;

       // private static int gridHeight = 5; // Y axis - unused for now.

        private static int vertCount = gridWidth * gridLength; // Total amount of verteces
        private static int indCount = (gridLength - 1) * (gridLength - 1) * 6; // Total amount of indeces

        private Vector3 camPosition, camLookAt, camUp;
        /* camPosition: The vector location the user-camera will be placed at.
         * camLookAt: The vector location the user-camera will be looking at.
         * camUp: The vector determines which direction is "up". (0,1,0) */

        CustomVertex.PositionColored[] verts = null; // Array to store the position & color values of different verteces.

        bool isMiddleMouseDown = false;

        private FillMode fillMode = FillMode.Point;
        private Color background_color = Color.Black;

        private bool invalidating = true;
        
        private static int[] indices = null; // Array to store the list of indices for the shapes we're creatin

        public Grid_3D()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true); // Sets the window control style.

            InitializeComponent(); // Base function used to initalize the form GUI.
            InitalizeGraphics(); // Invoke the graphics function to start the rendering of the device object which holds the vertex buffer for the shapes.
            InitalizeEventHandler(); // Function handles all the event handlers being invoked during run-time such as camera movement & vertex/index buffer creation.
        }

        private void InitalizeGraphics() // Function used to initalize the device graphics.
        {
            PresentParameters pp = new PresentParameters(); // Configures the parametrs for the presentation/window
            pp.Windowed = true; // Windowed for the form.
            pp.SwapEffect = SwapEffect.Discard;  // Configures how the memory in the back buffer is placed into the front buffer [Discarded through DirectX]

            pp.EnableAutoDepthStencil = true;
            pp.AutoDepthStencilFormat = DepthFormat.D16;

            device = new Device(0, DeviceType.Hardware, this, CreateFlags.HardwareVertexProcessing, pp); // Contructor for the device object
            /* 1) Adapter: This is the default device so its set to 0
             * 2) Type of device: Hardware
             * 3) Control location: sets where the graphics is being rendered [full form/panel/etc] -> thus referencing this panel.
             * 4) Flags: HardWareVertexProcessing uses the GPU to do the calculations
             * 5) Parameters: PP object references which sets the window style & swap effect.
             */

            GenerateVertex(); // Generates the list of verteces.
            GenerateIndex(); // Generates the list of indeces.

            vb = new VertexBuffer(typeof(CustomVertex.PositionColored), vertCount, device, Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionColored.Format, Pool.Default);
            /* Vertex Buffer Constructor - used to create a "memory" location which acts as a buffer for the vertex information used to render the graphical shapes.
             * Type: The style of vertex we're using - Position colored as it'll hold a location & color in the vertex.
             * VertexNumber: How many vertexes you'll be using
             * Device: Reference the device object.
             * Usage: How the library handles the buffer information - Dynamic will allow for us to be able to switch between the back & front buffer
             * Usage: Write-only to avoid any accidental overwrites/edits to the buffer during run-time.
             * Format: Reference the format styte used for PositionColored.
             * Pool: The default memory class that will hold the buffer as a resource.
             */

            OnVertexBufferCreate(vb, null); // Invoke the event.

            ib = new IndexBuffer(typeof(int), indCount, device, Usage.WriteOnly, Pool.Default);
            /* Index Buuffer Constructor - Array of pointers into the vertex buffer, allows to reorder the vertex data and reuse existing data for multiple vertices
             * Type: data primitive type
             * Size of the buffer [int]
             * Reference to the device object used
             * Usage: How the library handles the buffer information - WriteOnly allows for the data to be written over during runtime.
             * Pool: The default memory class that will hold the buffer as a resource - default used mostly.
             */

            OnIndexBufferCreate(ib, null); // Invoke the event

            // Initial Camera Position
            camPosition = new Vector3(5, 2.5f, -5.5f);
            camUp = new Vector3(0, 1, 0);
        }

        private void InitalizeEventHandler()
        {
            vb.Created += new EventHandler(OnVertexBufferCreate); // Subscribe the VertexBuffer to an event to invoke OnVertexBufferCreate function.
            ib.Created += new EventHandler(OnIndexBufferCreate); // Subscribe the IndexBuffer to an event to invoke OnIndexBufferCreate function.

            KeyDown += new KeyEventHandler(OnKeyDown); // Subscribe the KeyDown function to when a key is clicked on the keyboard.
            MouseWheel += new MouseEventHandler(OnMouseScroll); // Subscribe the OnMouseScroll function to when the mouse-scroll is used.

            MouseMove += new MouseEventHandler(OnMouseMove); // Subscribe the OnMouseMove function to when the mouse is moved.
            MouseDown += new MouseEventHandler(OnMouseDown);  // Subscribe the OnMouseMove function to when the mouse is moved down.
            MouseUp += new MouseEventHandler(OnMouseUp); // Subscribe the OnMouseMove function to when the mouse is moved up.
        }

        private void OnIndexBufferCreate(object sender, EventArgs e) // Function listens to when the event is invoked  [ib.Created += (this.OnIndexBufferCreate]
        {
            IndexBuffer buffer = (IndexBuffer)sender; // Create a copy of the original index buffer.
            buffer.SetData(indices, 0, LockFlags.None); // Set the data values from the indices array with no LockFlags.
        }

        private void OnVertexBufferCreate(object sender, EventArgs e) // Function listens to when the event is invoked. [vb.Created += (this.OnVertexBufferCreate)]
        {
            VertexBuffer buffer = (VertexBuffer)sender; // Create a copy of the original vertex buffer object.
            buffer.SetData(verts, 0, LockFlags.None); // Add the vertecs values into the buffer array.
        }

        private void SetupCamera() // Creates the camera which will control the location the user is looking at as well as the references for the grid coordinates.
        {
            // Handles the camera movement -> The looking at position has to be linked with the cameraPosition with a slight variation in the Y & Z axis.
            camLookAt.X = (float)Math.Sin(rotY) + camPosition.X + (float)(Math.Sin(rotXZ) * Math.Sin(rotY));
            camLookAt.Y = (float)Math.Sin(rotXZ) + camPosition.Y;
            camLookAt.Z = (float)Math.Cos(rotY) + camPosition.Z + (float)(Math.Sin(rotXZ) * Math.Cos(rotY));


            device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4, this.Width / this.Height, 1.0f, 100.0f);

            /* Transform.Projection: Handles the way the device DISPLAYS the projection to the user.
             * FieldOfView: PI/4 - Aspect Ratio: Width/Height
             * zPlaneNear: Configures the distance in the Z direction to how close the stuff we're looking at is being drawn
             * zPlaneFar: Configures the distance in the Z direction to how far the stuff we're gonna be able to look at are drawn. */

            device.Transform.View = Matrix.LookAtLH(camPosition, camLookAt, camUp); // Handles the user VIEW into the device object.
            // Parameters: 1) CameraPosition, cameraTarget, cameraUpVector

            device.RenderState.Lighting = false; // Disable lightning to be able to see the vertex colors - otherwise we'd have to set the VertexBuffer type to normal.
            device.RenderState.CullMode = Cull.CounterClockwise; // To allow the display of the depth.
            device.RenderState.FillMode = fillMode; // the format which the rendering will present to the user.

        }

        private void Grid_3D_Paint(object sender, PaintEventArgs e)
        {
            device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, background_color, 1, 0); // Clears the device information/display on each loop-iteration.

            SetupCamera(); // Invoke the user-camera.

            device.BeginScene(); // Start the scene-rendering.

            device.VertexFormat = CustomVertex.PositionColored.Format; // Informs the device object with the vertex-format values to be used.
            device.SetStreamSource(0, vb, 0); // Configures the source of the stream for the device object.
            device.Indices = ib; // Set the indices property to our IndexBuffer object which will handle the creation of the cube based on the vertexes provided.

            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertCount, 0, indCount / 3); // Handles drawing primative types based on a list of indices provided.

            device.EndScene(); // End the scene-rendering.

            device.Present(); // Send the scene to the windows-form.

            menuStrip1.Update(); // Shows the menu-strip upon initalization. 

            if(invalidating)
            {
                this.Invalidate(); // Function used to invalidate the window post function invokation in a recursive-style to keep the function on-loop. 
            }
        }
           
        private void GenerateVertex()
        {

           /* Vertex Calculation
             *  The location where the camera will be looking at will be set to 0 as a reference point to our axis.
             *  Each vertex holds an (x,y,z) value - the Z value will be in/out of the screen thus representing the depth.
             *  ToArgb() parses the value into an acceptable int format.
             * 
             *  2D Grid Loop setting: The outer loop will represents the width of the grid which will be represented by the Z axis.
             *  The inner loop will represents the length of the grid represented by the x axis.
             *  Currently we're not gonna use the Y axis as I've not created an algorithm for it but the 3rd loop will for it once implemented in the grid.
             *  the K will be incremented per full loop on both axis thus saving the position of each grid point.
            */

            verts = new CustomVertex.PositionColored[vertCount]; // Initalize the array for the verteces values.
            int k = 0;

            for (int z = 0; z < gridWidth; z++)
            {
                for (int x = 0; x < gridLength; x++)
                {
                    /*for (int y = 0; y < gridHeight; y++)
                    {*/
                        verts[k].Position = new Vector3(x, 0, z);
                        verts[k].Color = Color.White.ToArgb();
                        k++;
                    //}
                }
            }  
        }

        private void GenerateIndex()
        {
            /* 
             * Index Calculation:
             * Each grid-square is represented by 2 triangles connected together; thus each grid-square consists of 4 verteces.
             * Ex: If we only have 5 length & 5 width -> The first grid verteces will be: [ 5 6 ]  
             *                                                                            [ 0 1 ] 
             * To be able to properly draw the triangles & for them to appear on both sides, we have to follow an either counter-clock wise or clock wise pattern.
             * Algorithm:       ib= 
             *                  0, 5, 6, 0, 6, 1, ->k++
             *                  1, 6, 7, 1, 7, 2, ->k++
             *                  2, 7, 8, 2, 8, 3, ->k++
             *                  3, 8, 9, 3, 9, 4, ->k++ k++
             *                  ----------------
             *                  5, 10, 11, 5, 11, 6
             * Thus our for loop will start with setting the first one with 0, then 0+length = 5, then 0+length+1 = 6 [first triangle done] -> repeat once more until 5 [1st grid line].
             */

            indices = new int[indCount];
            int k = 0;
            int l = 0; 

            for (int i = 0; i < indCount; i += 6)
            {
                indices[i] = k;                                 // 
                indices[i + 1] = k + gridLength;                //       These 3 represent the first triangle      ]
                indices[i + 2] = k + gridLength + 1;            //                                                 ]         
                                                                //                                                 ]  -> Both combined form 1 grid square.
                indices[i + 3] = k;                             //                                                 ]
                indices[i + 4] = k + gridLength + 1;            //       These 3 represent the second triangle     ]
                indices[i + 5] = k + 1;                         //      

                k++;
                l++;

                if (l == gridLength-1) // check if the length has reached the max length, in that case: reset it.
                {
                    l = 0;
                    k++;
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) // Handles the camera movement via [WASD] 
        {
            switch (e.KeyCode)
            {
                case (Keys.W):  // Move forward on the Z axis
                    {
                        camPosition.X += moveSpeed * (float)Math.Sin(rotY);
                        camPosition.Z += moveSpeed * (float)Math.Cos(rotY);
                        break;
                    }
                case (Keys.S): // Move back on the Z axis
                    {
                        camPosition.X -= moveSpeed * (float)Math.Sin(rotY);
                        camPosition.Z -= moveSpeed * (float)Math.Cos(rotY);
                        break;
                    }

                case (Keys.D): // Move right on the X axis
                    {
                        camPosition.X += moveSpeed * (float)Math.Sin(rotY + Math.PI / 2);
                        camPosition.X += moveSpeed * (float)Math.Cos(rotY + Math.PI / 2);
                        break;
                    }
                case (Keys.A): // Move left on the X axis
                    {
                        camPosition.X -= moveSpeed * (float)Math.Sin(rotY + Math.PI / 2);
                        camPosition.X -= moveSpeed * (float)Math.Cos(rotY + Math.PI / 2);
                        break;
                    }
                case (Keys.Q): // Rotate left
                    {
                        rotY -= turnSpeed;
                        break;
                    }
                case (Keys.E): // Rotate right
                    {
                        rotY += turnSpeed;
                        break;
                    }
                case (Keys.Up): // Look up
                    {
                        if (rotXZ < Math.PI / 2) // if this value is > 90
                        {
                            rotXZ += turnSpeed;
                        }
                        break;
                    }
                case (Keys.Down): // Look down
                    {
                        if (rotXZ > -Math.PI / 2) // if this value is > -90
                        {
                            rotXZ -= turnSpeed;
                        }
                        break;
                    }
            }
        }

        private void OnMouseScroll(object sender, MouseEventArgs e) // Handles the camera movement for up & down [mouse wheel/scroll]
        {
            camPosition.Y -= e.Delta * 0.001f;
        }

        private void OnMouseMove(object sender, MouseEventArgs e) // Checks if the middle-mouse button is clicked, if so -> it resets the rotY location to the current one or the one saved in tempY
        {
            if(isMiddleMouseDown)
            {
                rotY = tempY + e.X * turnSpeed;

                float temp = tempXZ - e.Y * turnSpeed / 4; // temporary variable to keep calculating the data without any effects - only applies when the limits is reached.
                if(temp < Math.PI / 2 && temp > -Math.PI / 2) // Prevent movement of the camera up/down between 90 & -90 degrees.
                {
                    rotXZ = temp;
                }
            }
        }
         
        private void OnMouseDown(object sender, MouseEventArgs e) // Once the mouse scroll is clicked, the tempY buffer is used to save the current Y location to avoid jumping when going back.
        {
            switch (e.Button)
            {
                case (MouseButtons.Middle):
                    {
                        tempY = rotY - e.X * turnSpeed; // Save the inital Y location so when the mouse is released we can reset back to this point. 

                        tempXZ = rotXZ + e.Y * turnSpeed / 4;

                        isMiddleMouseDown = true;
                        break;
                    }
            }
        }

        private void wireFrameToolStripMenuItem_Click(object sender, EventArgs e) // Menu button for fillmode - wireframe.
        {
            fillMode = FillMode.WireFrame;
            wireFrameToolStripMenuItem.Checked = true;
            pointToolStripMenuItem.Checked = false;
        }

        private void pointToolStripMenuItem_Click(object sender, EventArgs e) // Menu button for fillmode - point.
        {
            fillMode = FillMode.Point;
            wireFrameToolStripMenuItem.Checked = false;
            pointToolStripMenuItem.Checked = true;
        }

        private void backgroundColorToolStripMenuItem_Click(object sender, EventArgs e) // Menu button for background color - color box.
        {
            ColorDialog BG_ColorDialog = new ColorDialog();

            invalidating = false; // disable the loop during the color-box display
            if (BG_ColorDialog.ShowDialog(this) == DialogResult.OK)
            {
                background_color = BG_ColorDialog.Color;
            }
            invalidating = true; // renable the loop
            this.Invalidate(); // invoke the loop.
        }

        private void OnMouseUp(object sender, MouseEventArgs e)  // Once the mouse scroll is released, it resets back so the rotY is reset to the original value.
        {
            switch (e.Button)
            {
                case (MouseButtons.Middle):
                    {
                        isMiddleMouseDown = false;
                        break;
                    }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
