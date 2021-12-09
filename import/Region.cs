﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

namespace import
{
	public class Region
	{
		public int X, Y;
		public bool isLoaded, inQueue;
		public string filename;
		public Chunk[,] chunks = new Chunk[32, 32];
        public BlockFormat blockFormat;
		public ConcurrentBag<Chunk> edgeChunks = new ConcurrentBag<Chunk>();

        /// <summary>Initializes a region, which contains a grid of 32x32 chunks. Use Load() to load the region into memory.</summary>
        /// <param name="filename">Filename of the region, used to load it into memory.</param>
        /// <param name="x">The x value of the region.</param>
        /// <param name="y">The y value of the region.</param>
        public Region(string filename, BlockFormat blockFormat, int x, int y)
		{
			this.filename = filename;
            this.blockFormat = blockFormat;
            X = x;
			Y = y;
			isLoaded = false;
			inQueue = false;
		}

		/// <summary>Loads the chunks of the region. Returns whether successful.</summary>
		public bool Load()
		{
			if (!File.Exists(filename))
				return false;

			frmImport main = ((frmImport)Application.OpenForms["frmImport"]);

			try
			{
				main.lblLoadingRegion.Invoke((MethodInvoker)(() => main.lblLoadingRegion.Visible = true));
			}
			catch (Exception e)
			{
				Thread.CurrentThread.Abort();
			}

			// Process region file
			// http://minecraft.gamepedia.com/Region_file_format
			try
			{
				using (FileStream fs = new FileStream(filename, FileMode.Open))
				{
					main.regionFileStream = fs;

					BinaryReader br = new BinaryReader(fs);
					List<int> chunkoff = new List<int>();
					for (int c = 0; c < 32 * 32; c++)
					{
						int off = Util.ReadInt24(br); // Offset (4KiB sectors)
						if (off > 0)
							chunkoff.Add(off);
						br.ReadByte(); // Sector count
					}

					// Process the chunks
					// http://minecraft.gamepedia.com/Chunk_format
					for (int c = 0; c < chunkoff.Count; c++)
					{
						try
						{
							main.lblLoadingRegion.Invoke((MethodInvoker)(() =>
								main.lblLoadingRegion.Text = main.GetText("loadingregion") + " (" + Math.Floor((float)c / (float)chunkoff.Count * 100.0) + "%)"
							));
						}
						catch (Exception e)
						{
							Thread.CurrentThread.Abort();
						}

						fs.Seek(chunkoff[c] * 4096, 0);
						int clen = Util.ReadInt32(br) - 1;
						br.ReadByte(); //Always 2

						// Decompress and read NBT structure
						br.ReadByte();
						br.ReadByte();

						Chunk chunk = new Chunk(br.ReadBytes(clen - 6), blockFormat);
						if (!chunk.Load())
							continue;

						chunks[Util.ModNeg(chunk.X, 32), Util.ModNeg(chunk.Y, 32)] = chunk;
					}
				}
				main.regionFileStream = null;
				isLoaded = true;

				foreach (Chunk c in edgeChunks)
                {
					if (c.XYImageInQueue)
						continue;

					main.ChunkImageXYQueue.Enqueue(c);
					
					if (c.XYImage != null)
					{
						c.XYImage.Image.Dispose();
						c.XYImage = null;
						c.XYImageInQueue = true;
					}
				}
				
			}
			catch (Exception e)
			{
				// If count is 0, then regions were likely cleared by opening a new world
				if (e.InnerException != null)
					if (e.InnerException.GetType() == typeof(IOException))
						MessageBox.Show(main.GetText("worldopened"));

				return false;
			}

			main.lblLoadingRegion.Invoke((MethodInvoker)(() => main.lblLoadingRegion.Visible = false));
			GC.Collect();
			GC.WaitForPendingFinalizers();

			return true;
		}

		/// <summary>Clears the region of all chunks.</summary>
		public void Clear()
		{
			for (int i = 0; i < 32; i++)
				for (int j = 0; j < 32; j++)
					chunks[i, j] = null;
		}
	}
}