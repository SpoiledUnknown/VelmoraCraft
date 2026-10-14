using OpenTK.Graphics.OpenGL;

namespace VoxelRenderEngine.src.Graphics
{
    public class ShaderProgram
    {
        public int ID;
        public ShaderProgram(string vertexShaderFilepath, string fragmentShaderFilepath)
        {
            //Create the shader program
            ID = GL.CreateProgram();

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, LoadShaderSource(vertexShaderFilepath));
            GL.CompileShader(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, LoadShaderSource(fragmentShaderFilepath));
            GL.CompileShader(fragmentShader);

            GL.AttachShader(ID, vertexShader);
            GL.AttachShader(ID, fragmentShader);

            GL.LinkProgram(ID);

            //Delete the Shaders
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        public void Bind() { GL.UseProgram(ID); }
        public void Delete() {  GL.DeleteShader(ID); }

        private static string LoadShaderSource(string filePath)
        {
            string shaderSource = "";

            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shaders", filePath);
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine(Directory.GetCurrentDirectory(), "Shaders", filePath);
                }
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine("../../../Shaders", filePath);
                }

                using StreamReader reader = new StreamReader(fullPath);
                shaderSource = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load shader source file: " + ex.Message);
            }

            return shaderSource;
        }
    }
}
