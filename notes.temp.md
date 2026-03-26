

in there are class definitions for differnt types of HTML "blocks".  Lets add some more blocks that are very common to layouts in CMS page design. 

- Video player (These shoudl all inhert from the existing EmbedBlock 
  - Youtube video player, 
  - Vimeo Player 
  - Twtich Video/Clip
      - https://dev.twitch.tv/docs/embed/video-and-clips/ 
- TikTok Video 
    - looks like an embed code - not sure what that is 
- Columns
    - This is actually a horizontal row that can have columns added to it via a collection property 
    - - layout (with variable # of columns based on grid - obviously up to 12)
- cards
    - again just inherts from the columns, but adds card specific data
    - with or without image on top
- Carousel
    - will hold an array/list of images to display. things like control location  
- Content Link
    - link to internal content


We will need to potentially add these to the  serializers:
  - src\Aero.Cms.Core\Blocks\Serialization

These new blocks should live in the following dir: src\Aero.Cms.Core\Blocks\Common


Contraints:

Don't edit any pages or add any HTML/Razor stuff just yet - just the c# code blocks for now.
